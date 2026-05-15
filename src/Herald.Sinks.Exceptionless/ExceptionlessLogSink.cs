// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;

namespace Herald.Sinks.Exceptionless;

/// <summary>
/// Sink that forwards Herald log events to the Exceptionless
/// error-tracking platform. Drop-in for Serilog.Sinks.Exceptionless.
/// Pure HTTP — no Exceptionless SDK dependency.
/// </summary>
public sealed class ExceptionlessLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable, INetworkSink
{
    private readonly Uri _endpoint;
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public ExceptionlessLogSink(
        string apiKey,
        string serverUrl = "https://collector.exceptionless.io",
        HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverUrl);

        _apiKey = apiKey;
        _endpoint = new Uri(serverUrl.TrimEnd('/') + "/api/v2/events");
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        LogBatch(new[] { logEvent });
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        var body = BuildBody(events);

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _apiKey);

        using var response = _httpClient.Send(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
    }

    private static string BuildBody(IReadOnlyList<LogEvent> events)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var evt in events)
            {
                WriteEvent(writer, evt);
            }
            writer.WriteEndArray();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteEvent(Utf8JsonWriter writer, LogEvent evt)
    {
        var hasException = evt.Context.TryGetValue(LogContextKeys.Exception, out var v) && v is Exception;
        writer.WriteStartObject();
        writer.WriteString("type", hasException ? "error" : "log");
        writer.WriteString("source", evt.Category.Value);
        writer.WriteString("date", evt.TimeUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteString("message", evt.Message ?? string.Empty);

        writer.WriteStartArray("tags");
        writer.WriteStringValue(evt.Level.Key);
        writer.WriteEndArray();

        writer.WriteStartObject("data");
        writer.WriteString("@level", evt.Level.Key);
        writer.WriteString("template", evt.MessageTemplate ?? string.Empty);
        if (evt.Properties is not null && evt.Properties.Count > 0)
        {
            foreach (var prop in evt.Properties)
            {
                writer.WriteString(prop.Name, prop.ResolvedValue?.ToString());
            }
        }
        writer.WriteEndObject();

        writer.WriteEndObject();
    }
}
