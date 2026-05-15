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

namespace Herald.Sinks.Dynatrace;

/// <summary>
/// Sink that posts log events to Dynatrace's Generic Log Ingest API.
/// Drop-in equivalent of Serilog.Sinks.Dynatrace. Pure HTTP — no
/// Dynatrace SDK dependency.
/// </summary>
/// <remarks>
/// <para>
/// <b>Wire format.</b>
/// <c>POST {environmentUrl}/api/v2/logs/ingest</c> with header
/// <c>Authorization: Api-Token {token}</c>. Body is a JSON array of
/// entries carrying <c>timestamp</c>, <c>severity</c>, <c>content</c>,
/// and any custom attributes.
/// </para>
/// <para>
/// <b>Limits.</b> Dynatrace caps each request at 5 MB payload and
/// 50,000 entries. At ~1 KB/event the batching layer's default caps
/// are well under those ceilings, but very-high-property events are
/// still worth a soak test.
/// </para>
/// </remarks>
public sealed class DynatraceLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable
{
    private readonly Uri _endpoint;
    private readonly string _apiToken;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public DynatraceLogSink(
        string environmentUrl,
        string apiToken,
        HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiToken);

        _endpoint = new Uri(environmentUrl.TrimEnd('/') + "/api/v2/logs/ingest");
        _apiToken = apiToken;
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
        request.Headers.TryAddWithoutValidation("Authorization", "Api-Token " + _apiToken);

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
                WriteEntry(writer, evt);
            }
            writer.WriteEndArray();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteEntry(Utf8JsonWriter writer, LogEvent evt)
    {
        writer.WriteStartObject();
        writer.WriteString("timestamp", evt.TimeUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteString("severity", MapSeverity(evt.Level.Key));
        writer.WriteString("content", evt.Message ?? string.Empty);
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
    }

    // Dynatrace uses a fixed severity vocabulary. Map Herald levels to the
    // closest DT severity so the log viewer colour-codes consistently.
    private static string MapSeverity(string levelKey) => levelKey switch
    {
        "trace" or "debug" => "DEBUG",
        "info" or "notice" or "success" => "INFO",
        "warn" => "WARN",
        "error" => "ERROR",
        "critical" or "security" => "CRITICAL",
        _ => "INFO",
    };
}
