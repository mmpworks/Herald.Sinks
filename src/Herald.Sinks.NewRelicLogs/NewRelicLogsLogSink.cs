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
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;

namespace Herald.Sinks.NewRelicLogs;

/// <summary>
/// Sink that ships Herald log events to the New Relic Logs ingest
/// API. Drop-in for Serilog.Sinks.NewRelic.Logs. Pure HTTP — no New
/// Relic SDK dependency.
/// </summary>
/// <remarks>
/// Default endpoint is the US region (<c>log-api.newrelic.com</c>).
/// EU accounts must override with <c>log-api.eu.newrelic.com</c>.
/// </remarks>
public sealed class NewRelicLogsLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable
{
    private readonly Uri _endpoint;
    private readonly string _licenseKey;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public NewRelicLogsLogSink(
        string licenseKey,
        string endpoint = "https://log-api.newrelic.com/log/v1",
        HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        _licenseKey = licenseKey;
        _endpoint = new Uri(endpoint);
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
        request.Headers.TryAddWithoutValidation("Api-Key", _licenseKey);

        using var response = _httpClient.Send(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
    }

    private static string BuildBody(IReadOnlyList<LogEvent> events)
    {
        // New Relic accepts either a flat array of log records or an
        // object with `common` + `logs`. Flat array is simpler and the
        // recommended shape for non-grouped batches.
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var evt in events)
            {
                WriteLog(writer, evt);
            }
            writer.WriteEndArray();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteLog(Utf8JsonWriter writer, LogEvent evt)
    {
        writer.WriteStartObject();
        writer.WriteNumber("timestamp", evt.TimeUtc.ToUnixTimeMilliseconds());
        writer.WriteString("message", evt.Message ?? string.Empty);
        writer.WriteString("logtype", evt.Level.Key);

        writer.WriteStartObject("attributes");
        writer.WriteString("level", evt.Level.Key);
        writer.WriteString("category", evt.Category.Value);
        writer.WriteString("template", evt.MessageTemplate ?? string.Empty);

        if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value) && value is Exception ex)
        {
            writer.WriteString("exception", ex.ToString());
            writer.WriteString("exception.type", ex.GetType().FullName ?? ex.GetType().Name);
        }

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
