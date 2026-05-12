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
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;

namespace Herald.Sinks.ElmahIo;

/// <summary>
/// Sink that ships Herald log events to elmah.io's error-tracking
/// service via the v3 messages bulk API. Drop-in for
/// Serilog.Sinks.ElmahIo. Pure HTTP — no elmah.io SDK.
/// </summary>
public sealed class ElmahIoLogSink : ILogger, IBatchedLogSink, IDisposable
{
    private readonly Uri _endpoint;
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public ElmahIoLogSink(string apiKey, string logId, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(logId);

        _apiKey = apiKey;
        _endpoint = new Uri($"https://api.elmah.io/v3/messages/{logId}/bulk");
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public void Log(LogEvent logEvent)
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
                WriteMessage(writer, evt);
            }
            writer.WriteEndArray();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteMessage(Utf8JsonWriter writer, LogEvent evt)
    {
        writer.WriteStartObject();
        writer.WriteString("dateTime", evt.TimeUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteString("severity", MapSeverity(evt.Level.Key));
        writer.WriteString("title", evt.Message ?? string.Empty);
        writer.WriteString("source", evt.Category.Value);
        writer.WriteString("hostname", Environment.MachineName);
        writer.WriteString("application", "herald");

        if (evt.Context.TryGetValue(LogContextKeys.Exception, out var v) && v is Exception ex)
        {
            writer.WriteString("type", ex.GetType().FullName ?? ex.GetType().Name);
            writer.WriteString("detail", ex.ToString());
        }

        if (evt.Properties is not null && evt.Properties.Count > 0)
        {
            writer.WriteStartArray("data");
            foreach (var prop in evt.Properties)
            {
                writer.WriteStartObject();
                writer.WriteString("key", prop.Name);
                writer.WriteString("value", prop.ResolvedValue?.ToString() ?? string.Empty);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static string MapSeverity(string levelKey) => levelKey switch
    {
        "trace" or "debug" => "Debug",
        "info" or "notice" or "success" => "Information",
        "warn" => "Warning",
        "error" => "Error",
        "critical" or "security" => "Fatal",
        _ => "Information",
    };
}
