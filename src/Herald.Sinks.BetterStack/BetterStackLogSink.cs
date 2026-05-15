// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.BetterStack;

/// <summary>
/// Sink that ships log events to Better Stack (formerly Logtail) via
/// their public ingest endpoint. Bearer-token auth, NDJSON body.
/// </summary>
public sealed class BetterStackLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable
{
    private const string DefaultEndpoint = "https://in.logs.betterstack.com/";

    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly string _token;
    private readonly bool _ownsHttp;

    public BetterStackLogSink(string sourceToken, string? endpoint = null, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceToken);
        _token = sourceToken;
        _endpoint = new Uri(endpoint ?? DefaultEndpoint, UriKind.Absolute);
        _ownsHttp = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public override void Log(LogEvent logEvent) => LogBatch(new[] { logEvent });

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        var body = BuildNdjson(events);
        using var content = new StringContent(body, Encoding.UTF8, "application/x-ndjson");
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        using var response = _http.SendAsync(request).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }

    private static string BuildNdjson(IReadOnlyList<LogEvent> events)
    {
        var sb = new StringBuilder(events.Count * 200);
        foreach (var evt in events)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("dt", evt.TimeUtc);
                writer.WriteString("level", evt.Level.Key);
                writer.WriteString("message", evt.Message ?? string.Empty);
                writer.WriteString("category", evt.Category.Value);
                writer.WriteString("template", evt.MessageTemplate ?? string.Empty);
                writer.WriteEndObject();
            }
            sb.Append(Encoding.UTF8.GetString(stream.ToArray()));
            sb.Append('\n');
        }
        return sb.ToString();
    }
}
