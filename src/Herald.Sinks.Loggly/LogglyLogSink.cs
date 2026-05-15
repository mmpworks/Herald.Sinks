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

namespace Herald.Sinks.Loggly;

/// <summary>
/// Sink that ships log events to Loggly via the bulk HTTP endpoint.
/// Drop-in for Serilog.Sinks.Loggly. Token travels in the URL; body
/// is NDJSON per Loggly's bulk-input spec.
/// </summary>
public sealed class LogglyLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable
{
    private readonly Uri _endpoint;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public LogglyLogSink(
        string customerToken,
        string? tag = null,
        HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerToken);

        var tagSegment = string.IsNullOrWhiteSpace(tag) ? "tag/herald/" : $"tag/{Uri.EscapeDataString(tag)}/";
        _endpoint = new Uri($"https://logs-01.loggly.com/bulk/{Uri.EscapeDataString(customerToken)}/{tagSegment}");
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

        var sb = new StringBuilder();
        foreach (var evt in events)
        {
            sb.Append(BuildJson(evt)).Append('\n');
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(sb.ToString(), Encoding.UTF8, "application/json"),
        };

        using var response = _httpClient.Send(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
    }

    private static string BuildJson(LogEvent evt)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("timestamp", evt.TimeUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("level", evt.Level.Key);
            writer.WriteString("category", evt.Category.Value);
            writer.WriteString("message", evt.Message ?? string.Empty);
            writer.WriteString("template", evt.MessageTemplate ?? string.Empty);

            if (evt.Context.TryGetValue(LogContextKeys.Exception, out var v) && v is Exception ex)
            {
                writer.WriteString("exception", ex.ToString());
            }

            if (evt.Properties is not null && evt.Properties.Count > 0)
            {
                foreach (var prop in evt.Properties)
                {
                    writer.WriteString(prop.Name, prop.ResolvedValue?.ToString());
                }
            }

            writer.WriteEndObject();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
