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

namespace Herald.Sinks.ApplicationInsightsHttp;

/// <summary>
/// Sink that posts log events to Azure Application Insights' ingestion
/// endpoint as <c>MessageData</c> telemetry envelopes. Accepts either a
/// modern AI connection string
/// (<c>InstrumentationKey=...;IngestionEndpoint=...</c>) or a bare
/// instrumentation-key GUID — the latter targets the classic global
/// endpoint.
/// </summary>
/// <remarks>
/// <para>
/// No <c>Microsoft.ApplicationInsights</c> SDK dependency. The sink
/// POSTs newline-agnostic JSON arrays of envelopes directly, same
/// pattern as <see cref="OtlpSinks.OtlpJsonLogSink"/>. That keeps Core's
/// dependency graph narrow and avoids coupling to AI SDK major version
/// churn.
/// </para>
/// <para>
/// The caller may supply an <see cref="HttpClient"/> for pooling or
/// test injection; if omitted, the sink owns a client with a 30 s
/// timeout. Ownership tracks the supplier.
/// </para>
/// <para>
/// Implements <see cref="IBatchedLogSink"/> — the sink serialises N
/// events into one AI request. Use a batching decorator upstream for
/// end-to-end batching; the sink itself does not buffer.
/// </para>
/// </remarks>
public sealed class ApplicationInsightsLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable, INetworkSink
{
    private readonly ApplicationInsightsConnectionString _connection;
    private readonly string? _roleName;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public ApplicationInsightsLogSink(
        string connectionString,
        string? roleName = null,
        HttpClient? httpClient = null)
    {
        _connection = ApplicationInsightsConnectionString.Parse(connectionString);
        _roleName = string.IsNullOrWhiteSpace(roleName) ? null : roleName;
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        LogBatch([logEvent]);
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        var payload = BuildEnvelopePayload(events);

        using var request = new HttpRequestMessage(HttpMethod.Post, _connection.TrackEndpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        using var response = _httpClient.Send(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    // ── Wire payload ──────────────────────────────────────────────

    private string BuildEnvelopePayload(IReadOnlyList<LogEvent> events)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartArray();
        foreach (var logEvent in events)
        {
            WriteEnvelope(writer, logEvent);
        }
        writer.WriteEndArray();

        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private void WriteEnvelope(Utf8JsonWriter writer, LogEvent logEvent)
    {
        writer.WriteStartObject();

        // Envelope header. The "name" selects the schema on AI's side;
        // "Microsoft.ApplicationInsights.Message" is the stable form
        // for the MessageData baseType.
        writer.WriteString("name", "Microsoft.ApplicationInsights.Message");
        writer.WriteString("time", logEvent.TimeUtc.UtcDateTime.ToString("O"));
        writer.WriteString("iKey", _connection.InstrumentationKey);

        WriteTags(writer, logEvent);
        WriteData(writer, logEvent);

        writer.WriteEndObject();
    }

    private void WriteTags(Utf8JsonWriter writer, LogEvent logEvent)
    {
        writer.WriteStartObject("tags");

        if (_roleName is not null)
        {
            writer.WriteString("ai.cloud.role", _roleName);
        }

        // Propagate W3C trace context so AI's distributed-trace view
        // links log entries to their owning operation.
        if (logEvent.Context.TryGetValue(LogContextKeys.TraceId, out var traceId) && traceId is string traceIdStr)
        {
            writer.WriteString("ai.operation.id", traceIdStr);
        }
        if (logEvent.Context.TryGetValue(LogContextKeys.SpanId, out var spanId) && spanId is string spanIdStr)
        {
            writer.WriteString("ai.operation.parentId", spanIdStr);
        }

        writer.WriteEndObject();
    }

    private static void WriteData(Utf8JsonWriter writer, LogEvent logEvent)
    {
        writer.WriteStartObject("data");
        writer.WriteString("baseType", "MessageData");

        writer.WriteStartObject("baseData");
        writer.WriteNumber("ver", 2);
        writer.WriteString("message", logEvent.Message);
        writer.WriteNumber(
            "severityLevel",
            ApplicationInsightsSeverityMapper.MapSeverityLevel(logEvent.Level));

        WriteProperties(writer, logEvent);

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteProperties(Utf8JsonWriter writer, LogEvent logEvent)
    {
        // AI "customDimensions" are the property bag. Serialized as
        // string-valued per AI's MessageData schema. Properties and
        // context each contribute; context keys that duplicate property
        // names are skipped so the property value wins.
        writer.WriteStartObject("properties");

        writer.WriteString("category", logEvent.Category.Value);
        writer.WriteString("messageTemplate", logEvent.MessageTemplate);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "category",
            "messageTemplate"
        };

        foreach (var property in logEvent.Properties)
        {
            if (seen.Add(property.Name))
            {
                writer.WriteString(property.Name, property.ResolvedValue?.ToString() ?? "null");
            }
        }

        foreach (var pair in logEvent.Context)
        {
            // Trace context already projected into tags; skip to avoid
            // duplicating it in customDimensions.
            if (pair.Key is LogContextKeys.TraceId or LogContextKeys.SpanId)
            {
                continue;
            }

            if (pair.Key == LogContextKeys.Exception && pair.Value is Exception ex)
            {
                writer.WriteString("exception.type", ex.GetType().FullName ?? ex.GetType().Name);
                writer.WriteString("exception.message", ex.Message);
                writer.WriteString("exception.stacktrace", ex.StackTrace ?? "");
                continue;
            }

            if (seen.Add(pair.Key))
            {
                writer.WriteString(pair.Key, pair.Value?.ToString() ?? "null");
            }
        }

        writer.WriteEndObject();
    }
}
