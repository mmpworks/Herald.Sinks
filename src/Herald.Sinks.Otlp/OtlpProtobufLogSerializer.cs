// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using Herald.Sinks.Otlp.Otlp;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Serialization;
using MMP.Herald.Services;

namespace Herald.Sinks.Otlp;

/// <summary>
/// Serializes LogEvent instances to OTLP protobuf binary format.
/// Uses OtlpProtobufWriter for low-level wire format encoding.
/// Field mapping matches the existing OtlpJsonLogSink for consistency.
/// </summary>
public sealed class OtlpProtobufLogSerializer : ILogEventSerializer<byte[]>
{
    private readonly ILogLevelRegistry _levelRegistry;
    private readonly IReadOnlyDictionary<string, string> _resourceAttributes;

    public OtlpProtobufLogSerializer(
        ILogLevelRegistry levelRegistry,
        IReadOnlyDictionary<string, string>? resourceAttributes = null)
    {
        _levelRegistry = levelRegistry ?? throw new ArgumentNullException(nameof(levelRegistry));
        _resourceAttributes = resourceAttributes ?? new Dictionary<string, string>
        {
            [OtlpDefaults.ServiceNameKey] = OtlpDefaults.UnknownService
        };
    }

    public byte[] Serialize(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        return SerializeBatch([logEvent]);
    }

    public byte[] SerializeBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var recordWriters = new Action<OtlpProtobufWriter>[events.Count];
        for (var i = 0; i < events.Count; i++)
        {
            var evt = events[i];
            recordWriters[i] = writer => WriteLogRecord(writer, evt);
        }

        return OtlpPayloadBuilder.Build(WriteResource, recordWriters);
    }

    private void WriteResource(OtlpProtobufWriter writer)
    {
        // Resource.Field 1: repeated KeyValue attributes
        foreach (var attr in _resourceAttributes)
        {
            OtlpPayloadBuilder.WriteStringAttribute(writer, 1, attr.Key, attr.Value);
        }
    }

    private void WriteLogRecord(OtlpProtobufWriter writer, LogEvent logEvent)
    {
        // LogRecord field numbers per OTLP spec:
        // 1: fixed64 time_unix_nano
        // 2: enum SeverityNumber
        // 3: string severity_text
        // 5: AnyValue body
        // 6: repeated KeyValue attributes
        // 9: bytes trace_id
        // 10: bytes span_id

        var unixNano = (ulong)(logEvent.TimeUtc.ToUnixTimeMilliseconds() * 1_000_000L);
        writer.WriteFixed64(1, unixNano);
        writer.WriteEnum(2, OtlpSeverityMapper.MapSeverityNumber(logEvent.Level, _levelRegistry));
        writer.WriteString(3, logEvent.Level.DisplayName);

        // Field 5: body (AnyValue with string_value)
        writer.WriteMessage(5, body =>
        {
            body.WriteString(1, logEvent.Message);
        });

        // Field 6: repeated KeyValue attributes
        OtlpPayloadBuilder.WriteStringAttribute(writer, 6, "log.category", logEvent.Category.Value);
        OtlpPayloadBuilder.WriteStringAttribute(writer, 6, "log.message_template", logEvent.MessageTemplate);

        foreach (var property in logEvent.Properties)
        {
            OtlpPayloadBuilder.WriteStringAttribute(writer, 6,
                property.Name, property.ResolvedValue?.ToString() ?? "null");
        }

        foreach (var pair in logEvent.Context)
        {
            if (pair.Key is LogContextKeys.TraceId or LogContextKeys.SpanId or LogContextKeys.Exception) continue;
            OtlpPayloadBuilder.WriteStringAttribute(writer, 6,
                pair.Key, pair.Value?.ToString() ?? "null");
        }

        // Exception attributes
        if (logEvent.Context.TryGetValue(LogContextKeys.Exception, out var exceptionValue) && exceptionValue is Exception ex)
        {
            OtlpPayloadBuilder.WriteStringAttribute(writer, 6,
                "exception.type", ex.GetType().FullName ?? ex.GetType().Name);
            OtlpPayloadBuilder.WriteStringAttribute(writer, 6,
                "exception.message", ex.Message);
            OtlpPayloadBuilder.WriteStringAttribute(writer, 6,
                "exception.stacktrace", ex.StackTrace ?? "");
        }

        // Trace context
        if (logEvent.Context.TryGetValue(LogContextKeys.TraceId, out var traceId) && traceId is string traceIdStr)
        {
            writer.WriteBytes(9, ParseHexBytes(traceIdStr));
        }

        if (logEvent.Context.TryGetValue(LogContextKeys.SpanId, out var spanId) && spanId is string spanIdStr)
        {
            writer.WriteBytes(10, ParseHexBytes(spanIdStr));
        }
    }

    // Per-event path: called once per TraceId and once per SpanId on every
    // log event that carries distributed-trace context. The old form wrapped
    // Convert.FromHexString in try/catch so a malformed ID degraded to an
    // empty byte[] instead of propagating. That cost was real — a throw on
    // a malformed ID is 5-50µs and a try block inhibits loop-body inlining
    // even when no exception fires. .NET 8 has no Convert.TryFromHexString,
    // so this is a hand-rolled nibble-parse: branch-predictable, inlineable,
    // no exception path.
    private static byte[] ParseHexBytes(string hex)
    {
        if (string.IsNullOrEmpty(hex) || (hex.Length & 1) != 0) return [];
        var length = hex.Length >> 1;
        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
        {
            var hi = HexNibble(hex[i << 1]);
            var lo = HexNibble(hex[(i << 1) + 1]);
            if (hi < 0 || lo < 0) return [];
            bytes[i] = (byte)((hi << 4) | lo);
        }
        return bytes;
    }

    private static int HexNibble(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1,
    };

    // Severity mapping delegated to OtlpSeverityMapper (shared with OtlpJsonLogSink)
}
