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

namespace Herald.Sinks.Axiom;

/// <summary>
/// Sink that ships log events to Axiom via the public ingest endpoint
/// (api.axiom.co/v1/datasets/{dataset}/ingest). Bearer-token auth, JSON
/// array body. HTTP-only.
/// </summary>
public sealed class AxiomLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable
{
    private const string DefaultBase = "https://api.axiom.co/v1/datasets/";

    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly string _token;
    private readonly bool _ownsHttp;

    public AxiomLogSink(string apiToken, string dataset, string? baseUrl = null, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset);
        _token = apiToken;
        _endpoint = new Uri((baseUrl ?? DefaultBase) + dataset + "/ingest", UriKind.Absolute);
        _ownsHttp = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public override void Log(LogEvent logEvent) => LogBatch(new[] { logEvent });

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        var body = BuildBody(events);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
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
            writer.WriteStartArray();
            foreach (var evt in events)
            {
                writer.WriteStartObject();
                writer.WriteString("_time", evt.TimeUtc);
                writer.WriteString("level", evt.Level.Key);
                writer.WriteString("category", evt.Category.Value);
                writer.WriteString("message", evt.Message ?? string.Empty);
                writer.WriteString("template", evt.MessageTemplate ?? string.Empty);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
