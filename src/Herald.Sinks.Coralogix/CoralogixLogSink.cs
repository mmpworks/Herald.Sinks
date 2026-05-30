// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.Coralogix;

/// <summary>
/// Sink that ships log events to Coralogix via the bulk-logs ingest
/// endpoint. Private-key auth in the body envelope; HTTP-only.
/// </summary>
public sealed class CoralogixLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable, INetworkSink
{
    private const string DefaultEndpoint = "https://ingress.coralogix.com/api/v1/logs";

    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly string _privateKey;
    private readonly string _applicationName;
    private readonly string _subsystemName;
    private readonly bool _ownsHttp;

    public CoralogixLogSink(string privateKey, string applicationName, string subsystemName,
        string? endpoint = null, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(subsystemName);
        _privateKey = privateKey;
        _applicationName = applicationName;
        _subsystemName = subsystemName;
        _endpoint = new Uri(endpoint ?? DefaultEndpoint, UriKind.Absolute);
        _ownsHttp = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public override void Log(LogEvent logEvent) => LogBatch(new[] { logEvent });

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        using var request = BuildRequest(events);
        // True synchronous send — no captured-context dependency, so this is
        // deadlock-safe on a SynchronizationContext-bearing thread.
        using var response = _http.Send(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();
    }

    public ValueTask LogAsync(LogEvent logEvent, CancellationToken cancellationToken = default) =>
        LogBatchAsync(new[] { logEvent }, cancellationToken);

    public async ValueTask LogBatchAsync(IReadOnlyList<LogEvent> events, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        using var request = BuildRequest(events);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage BuildRequest(IReadOnlyList<LogEvent> events)
    {
        var content = new StringContent(BuildBody(events), Encoding.UTF8, "application/json");
        return new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = content };
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }

    private string BuildBody(IReadOnlyList<LogEvent> events)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("privateKey", _privateKey);
            writer.WriteString("applicationName", _applicationName);
            writer.WriteString("subsystemName", _subsystemName);

            writer.WriteStartArray("logEntries");
            foreach (var evt in events)
            {
                writer.WriteStartObject();
                writer.WriteNumber("timestamp", evt.TimeUtc.ToUnixTimeMilliseconds());
                writer.WriteNumber("severity", MapSeverity(evt.Level.Key));
                writer.WriteString("category", evt.Category.Value);
                writer.WriteString("text", evt.Message ?? string.Empty);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static int MapSeverity(string levelKey) => levelKey switch
    {
        "trace" or "debug" => 1,
        "info" or "notice" => 3,
        "warn" => 4,
        "error" => 5,
        "critical" or "security" => 6,
        _ => 3,
    };
}
