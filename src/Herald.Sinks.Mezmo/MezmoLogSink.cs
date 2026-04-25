// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MMP.Herald;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.Mezmo;

/// <summary>
/// Sink that ships log events to Mezmo (LogDNA) via the public ingest
/// endpoint. Basic-auth (ingest key as username); HTTP-only.
/// </summary>
public sealed class MezmoLogSink : ILogger, IBatchedLogSink, IDisposable
{
    private const string DefaultEndpoint = "https://logs.logdna.com/logs/ingest";

    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly string _ingestKey;
    private readonly string _hostname;
    private readonly bool _ownsHttp;

    public MezmoLogSink(string ingestKey, string hostname, string? endpoint = null, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ingestKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);
        _ingestKey = ingestKey;
        _hostname = hostname;
        _endpoint = new Uri((endpoint ?? DefaultEndpoint) + $"?hostname={Uri.EscapeDataString(hostname)}", UriKind.Absolute);
        _ownsHttp = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public void Log(LogEvent logEvent) => LogBatch(new[] { logEvent });

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        var body = BuildBody(events);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = content };
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(_ingestKey + ":"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
        using var response = _http.SendAsync(request).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }

    private static string BuildBody(IReadOnlyList<LogEvent> events)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("lines");
            foreach (var evt in events)
            {
                writer.WriteStartObject();
                writer.WriteNumber("timestamp", evt.TimeUtc.ToUnixTimeMilliseconds());
                writer.WriteString("level", evt.Level.Key);
                writer.WriteString("app", evt.Category.Value);
                writer.WriteString("line", evt.Message ?? string.Empty);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
