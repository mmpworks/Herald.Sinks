// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.InfluxDB;

/// <summary>
/// Sink that writes log events to InfluxDB v2 via line protocol over
/// HTTP. No SDK dependency — line protocol is small and well-documented.
/// </summary>
/// <remarks>
/// Each event becomes one line: <c>logs,level=info,category=Auth message="..." 1234567890</c>.
/// Tags are <c>level</c> and <c>category</c>; <c>message</c> rides as a field.
/// </remarks>
public sealed class InfluxDBLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable, INetworkSink
{
    private readonly HttpClient _http;
    private readonly Uri _writeEndpoint;
    private readonly string _token;
    private readonly bool _ownsHttp;

    public InfluxDBLogSink(string serverUrl, string organization, string bucket, string token,
        HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(organization);
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        _writeEndpoint = new Uri(
            $"{serverUrl.TrimEnd('/')}/api/v2/write?org={Uri.EscapeDataString(organization)}&bucket={Uri.EscapeDataString(bucket)}&precision=ms",
            UriKind.Absolute);
        _token = token;
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
        var content = new StringContent(BuildLineProtocol(events), Encoding.UTF8, "text/plain");
        var request = new HttpRequestMessage(HttpMethod.Post, _writeEndpoint) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Token", _token);
        return request;
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }

    private static string BuildLineProtocol(IReadOnlyList<LogEvent> events)
    {
        var sb = new StringBuilder(events.Count * 200);
        foreach (var evt in events)
        {
            // Measurement,tag1=val,tag2=val field1="val" timestamp_ms
            sb.Append("logs");
            AppendTag(sb, "level", evt.Level.Key);
            AppendTag(sb, "category", evt.Category.Value);
            sb.Append(' ');
            sb.Append("message=\"");
            sb.Append(EscapeFieldValue(evt.Message ?? string.Empty));
            sb.Append("\"");
            sb.Append(' ');
            sb.Append(evt.TimeUtc.ToUnixTimeMilliseconds());
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static void AppendTag(StringBuilder sb, string key, string value)
    {
        sb.Append(',');
        sb.Append(EscapeTag(key));
        sb.Append('=');
        sb.Append(EscapeTag(value));
    }

    private static string EscapeTag(string value) =>
        value.Replace(",", "\\,").Replace("=", "\\=").Replace(" ", "\\ ");

    private static string EscapeFieldValue(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
