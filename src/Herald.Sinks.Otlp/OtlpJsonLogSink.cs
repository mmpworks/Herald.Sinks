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
using MMP.Herald.Levels;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;

namespace Herald.Sinks.Otlp;

/// <summary>
/// Sink that posts log events as OTLP JSON to an OpenTelemetry collector endpoint.
/// Maps LogEvent fields to OTLP semantic conventions:
/// - timeUnixNano, severityNumber, severityText, body, attributes, traceId, spanId.
/// Posts to {endpoint}/v1/logs.
/// The HttpClient is pooled internally (SocketsHttpHandler manages connection reuse).
/// If the caller provides an HttpClient, the sink does not own it and will not dispose it.
/// </summary>
public sealed class OtlpJsonLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable, INetworkSink
{
    private readonly Uri _endpoint;
    private readonly ILogLevelRegistry _levelRegistry;
    private readonly IReadOnlyDictionary<string, string> _resourceAttributes;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public OtlpJsonLogSink(
        string endpoint,
        ILogLevelRegistry levelRegistry,
        IReadOnlyDictionary<string, string>? resourceAttributes = null,
        HttpClient? httpClient = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentNullException.ThrowIfNull(levelRegistry);

        _endpoint = new Uri(endpoint.TrimEnd('/') + "/v1/logs", UriKind.Absolute);
        _levelRegistry = levelRegistry;
        _resourceAttributes = resourceAttributes ?? new Dictionary<string, string>
        {
            [OtlpDefaults.ServiceNameKey] = OtlpDefaults.UnknownService
        };
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public override void Log(LogEvent logEvent) {
        ArgumentNullException.ThrowIfNull(logEvent);
        LogBatch([logEvent]);
    }

    public void LogBatch(IReadOnlyList<LogEvent> events) {
        ArgumentNullException.ThrowIfNull(events);

        if (events.Count == 0)
        {
            return;
        }

        var payload = BuildOtlpPayload(events);

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        using var response = _httpClient.Send(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private string BuildOtlpPayload(IReadOnlyList<LogEvent> events) {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteStartArray("resourceLogs");
        writer.WriteStartObject();

        WriteResource(writer);

        writer.WriteStartArray("scopeLogs");
        writer.WriteStartObject();
        writer.WriteStartArray("logRecords");

        foreach (var logEvent in events)
        {
            WriteLogRecord(writer, logEvent);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndArray();

        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WriteEndObject();

        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private void WriteResource(Utf8JsonWriter writer) {
        writer.WriteStartObject("resource");
        writer.WriteStartArray("attributes");

        foreach (var attr in _resourceAttributes)
        {
            WriteStringAttribute(writer, attr.Key, attr.Value);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private void WriteLogRecord(Utf8JsonWriter writer, LogEvent logEvent) {
        writer.WriteStartObject();

        // Full-tick nanos: 1 tick = 100ns. ToUnixTimeMilliseconds() truncated
        // sub-millisecond precision; the tick-based form preserves precision to
        // the DateTimeOffset 100ns floor (F2).
        var unixNano = (logEvent.TimeUtc - DateTimeOffset.UnixEpoch).Ticks * 100L;
        writer.WriteString("timeUnixNano", unixNano.ToString());

        var severityNumber = OtlpSeverityMapper.MapSeverityNumber(logEvent.Level, _levelRegistry);
        writer.WriteNumber("severityNumber", severityNumber);
        // Uppercase severity text to match the OTLP convention and Herald's
        // OSS-side OtelLogRecord encoder (F3).
        writer.WriteString("severityText", logEvent.Level.DisplayName.ToUpperInvariant());

        writer.WriteStartObject("body");
        writer.WriteString("stringValue", logEvent.Message);
        writer.WriteEndObject();

        WriteAttributes(writer, logEvent);
        WriteTraceContext(writer, logEvent);

        writer.WriteEndObject();
    }

    private void WriteAttributes(Utf8JsonWriter writer, LogEvent logEvent) {
        writer.WriteStartArray("attributes");

        WriteStringAttribute(writer, "log.category", logEvent.Category.Value);
        WriteStringAttribute(writer, "log.message_template", logEvent.MessageTemplate);

        foreach (var property in logEvent.Properties)
        {
            // Type-dispatch on the resolved value so int/double/bool survive as
            // their AnyValue kind instead of collapsing to stringValue (F1).
            WriteAttribute(writer, property.Name, property.ResolvedValue);
        }

        foreach (var pair in logEvent.Context)
        {
            if (pair.Key is LogContextKeys.TraceId or LogContextKeys.SpanId or LogContextKeys.Exception)
            {
                continue;
            }

            WriteAttribute(writer, pair.Key, pair.Value);
        }

        if (logEvent.Context.TryGetValue(LogContextKeys.Exception, out var exceptionValue) && exceptionValue is Exception ex)
        {
            WriteStringAttribute(writer, "exception.type", ex.GetType().FullName ?? ex.GetType().Name);
            WriteStringAttribute(writer, "exception.message", ex.Message);
            WriteStringAttribute(writer, "exception.stacktrace", ex.StackTrace ?? "");
        }

        writer.WriteEndArray();
    }

    private static void WriteTraceContext(Utf8JsonWriter writer, LogEvent logEvent) {
        if (logEvent.Context.TryGetValue(LogContextKeys.TraceId, out var traceId) && traceId is string traceIdStr)
        {
            writer.WriteString("traceId", traceIdStr);
        }

        if (logEvent.Context.TryGetValue(LogContextKeys.SpanId, out var spanId) && spanId is string spanIdStr)
        {
            writer.WriteString("spanId", spanIdStr);
        }
    }

    private static void WriteStringAttribute(Utf8JsonWriter writer, string key, string value) {
        writer.WriteStartObject();
        writer.WriteString("key", key);
        writer.WriteStartObject("value");
        writer.WriteString("stringValue", value);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    // Type-dispatching attribute writer: picks the OTLP AnyValue JSON kind from
    // the runtime type of the value so int/double/bool round-trip as their type
    // rather than as a string. long/int -> intValue, double/float -> doubleValue,
    // bool -> boolValue, string -> stringValue, anything else -> stringValue via
    // ToString(). intValue is emitted as a bare JSON number: the OSS decoder
    // accepts both bare-number and string forms (OtlpJsonDecoder.ReadAnyValue),
    // and the bare number matches what Herald's own encoder produces.
    private static void WriteAttribute(Utf8JsonWriter writer, string key, object? value) {
        writer.WriteStartObject();
        writer.WriteString("key", key);
        writer.WriteStartObject("value");
        switch (value)
        {
            case long l:
                writer.WriteNumber("intValue", l);
                break;
            case int i:
                writer.WriteNumber("intValue", i);
                break;
            case double d:
                writer.WriteNumber("doubleValue", d);
                break;
            case float f:
                writer.WriteNumber("doubleValue", f);
                break;
            case bool b:
                writer.WriteBoolean("boolValue", b);
                break;
            case string s:
                writer.WriteString("stringValue", s);
                break;
            case null:
                writer.WriteString("stringValue", "null");
                break;
            default:
                writer.WriteString("stringValue", value.ToString() ?? "null");
                break;
        }
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    // Severity mapping delegated to OtlpSeverityMapper (shared with OtlpProtobufLogSerializer)
}
