// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.IO;
using Google.Protobuf;

namespace Herald.Sinks.Otlp.Otlp;

/// <summary>
/// Low-level writer that emits OTLP log records as raw protobuf wire format.
/// Uses Google.Protobuf's CodedOutputStream directly - no generated message classes needed.
/// Field numbers and wire types match the official OTLP .proto definitions.
///
/// This approach avoids the ceremony of implementing IMessage/IBufferMessage for every
/// OTLP message type while still producing spec-compliant protobuf binary. It also keeps
/// the package AOT-clean — generated message classes carry reflection descriptors the
/// trim analyzer can't prove safe.
/// </summary>
internal sealed class OtlpProtobufWriter : IDisposable
{
    private readonly MemoryStream _stream;
    private readonly CodedOutputStream _output;

    public OtlpProtobufWriter()
    {
        _stream = new MemoryStream();
        _output = new CodedOutputStream(_stream);
    }

    public byte[] ToByteArray()
    {
        _output.Flush();
        return _stream.ToArray();
    }

    public void Dispose()
    {
        _output.Dispose();
        _stream.Dispose();
    }

    // --- Wire format helpers ---
    // Protobuf tag = (field_number << 3) | wire_type
    // Wire types: 0=varint, 1=fixed64, 2=length-delimited

    /// <summary>
    /// Write a nested message as a length-delimited field.
    /// The action writes the message content to a sub-writer, then the bytes
    /// are emitted as a length-prefixed field.
    /// </summary>
    public void WriteMessage(int fieldNumber, Action<OtlpProtobufWriter> writeContent)
    {
        using var sub = new OtlpProtobufWriter();
        writeContent(sub);
        var bytes = sub.ToByteArray();

        WriteTag(fieldNumber, 2); // length-delimited
        _output.WriteBytes(ByteString.CopyFrom(bytes));
    }

    public void WriteString(int fieldNumber, string value)
    {
        if (value.Length == 0) return;
        WriteTag(fieldNumber, 2);
        _output.WriteString(value);
    }

    /// <summary>
    /// Write a string field unconditionally, including the empty string.
    /// Proto3 omits scalar defaults, and <see cref="WriteString"/> follows
    /// that rule. The OTLP AnyValue string_value oneof is the exception:
    /// an empty string attribute must round-trip as an explicit empty
    /// string_value, not as an absent field that the decoder would read
    /// back as null. Scope this unconditional write to the AnyValue path
    /// only — the general scalar-default omission stays intact elsewhere.
    /// </summary>
    public void WriteStringUnconditional(int fieldNumber, string value)
    {
        WriteTag(fieldNumber, 2);
        _output.WriteString(value);
    }

    public void WriteInt64(int fieldNumber, long value)
    {
        WriteTag(fieldNumber, 0); // varint
        _output.WriteInt64(value);
    }

    public void WriteDouble(int fieldNumber, double value)
    {
        WriteTag(fieldNumber, 1); // fixed64
        _output.WriteDouble(value);
    }

    public void WriteBool(int fieldNumber, bool value)
    {
        WriteTag(fieldNumber, 0); // varint
        _output.WriteBool(value);
    }

    public void WriteFixed64(int fieldNumber, ulong value)
    {
        if (value == 0) return;
        WriteTag(fieldNumber, 1);
        _output.WriteFixed64(value);
    }

    public void WriteEnum(int fieldNumber, int value)
    {
        if (value == 0) return;
        WriteTag(fieldNumber, 0);
        _output.WriteEnum(value);
    }

    public void WriteBytes(int fieldNumber, byte[] value)
    {
        if (value.Length == 0) return;
        WriteTag(fieldNumber, 2);
        _output.WriteBytes(ByteString.CopyFrom(value));
    }

    private void WriteTag(int fieldNumber, int wireType)
    {
        _output.WriteTag((uint)((fieldNumber << 3) | wireType));
    }
}

/// <summary>
/// Builds OTLP ExportLogsServiceRequest protobuf payloads from structured data.
/// Encapsulates the OTLP message hierarchy:
///   ExportLogsServiceRequest > ResourceLogs > ScopeLogs > LogRecord
/// </summary>
internal static class OtlpPayloadBuilder
{
    /// <summary>
    /// Build a complete ExportLogsServiceRequest containing one ResourceLogs
    /// with one ScopeLogs containing all provided log records.
    /// </summary>
    public static byte[] Build(
        Action<OtlpProtobufWriter> writeResource,
        Action<OtlpProtobufWriter>[] writeLogRecords)
    {
        using var root = new OtlpProtobufWriter();

        // Field 1: repeated ResourceLogs
        root.WriteMessage(1, resourceLogs =>
        {
            // ResourceLogs.Field 1: Resource
            resourceLogs.WriteMessage(1, writeResource);

            // ResourceLogs.Field 2: repeated ScopeLogs
            resourceLogs.WriteMessage(2, scopeLogs =>
            {
                // ScopeLogs.Field 2: repeated LogRecord
                foreach (var writeRecord in writeLogRecords)
                {
                    scopeLogs.WriteMessage(2, writeRecord);
                }
            });
        });

        return root.ToByteArray();
    }

    /// <summary>
    /// Write a KeyValue attribute pair whose value type is chosen from the
    /// runtime type of <paramref name="value"/>. Mirrors the JSON sink's
    /// type-dispatch so both wire formats carry attributes as the AnyValue
    /// kind the decoder expects, rather than collapsing everything to a
    /// string. AnyValue oneof field numbers (per the OTLP .proto):
    ///   1 = string_value, 2 = bool_value, 3 = int_value, 4 = double_value.
    /// Unhandled types fall back to string_value via ToString().
    /// </summary>
    public static void WriteAttribute(OtlpProtobufWriter writer, int fieldNumber, string key, object? value)
    {
        writer.WriteMessage(fieldNumber, kv =>
        {
            kv.WriteString(1, key);      // KeyValue.key
            kv.WriteMessage(2, WriteAnyValue(value)); // KeyValue.value (AnyValue)
        });
    }

    // Picks the AnyValue scalar kind from the runtime type. long/int -> int_value
    // (field 3), double/float -> double_value (field 4), bool -> bool_value
    // (field 2), string -> string_value (field 1), anything else -> string_value
    // via ToString(). int_value / double_value / bool_value are written
    // unconditionally (no proto3 default omission) so a 0 / 0.0 / false
    // attribute round-trips as its declared type instead of decoding to null.
    private static Action<OtlpProtobufWriter> WriteAnyValue(object? value) => value switch
    {
        long l => anyVal => anyVal.WriteInt64(3, l),
        int i => anyVal => anyVal.WriteInt64(3, i),
        double d => anyVal => anyVal.WriteDouble(4, d),
        float f => anyVal => anyVal.WriteDouble(4, f),
        bool b => anyVal => anyVal.WriteBool(2, b),
        string s => anyVal => anyVal.WriteStringUnconditional(1, s),
        null => anyVal => anyVal.WriteStringUnconditional(1, "null"),
        _ => anyVal => anyVal.WriteStringUnconditional(1, value.ToString() ?? "null"),
    };

    /// <summary>
    /// Write a KeyValue attribute pair with a string value. Retained for the
    /// call sites whose value is string by source type (resource attributes,
    /// log.category, message template, exception fields). Routes the
    /// AnyValue string_value through the unconditional write so an empty
    /// string round-trips as an explicit empty string_value rather than an
    /// omitted field (F4).
    /// KeyValue: field 1 = key (string), field 2 = AnyValue
    /// AnyValue: field 1 = string_value
    /// </summary>
    public static void WriteStringAttribute(OtlpProtobufWriter writer, int fieldNumber, string key, string value)
    {
        writer.WriteMessage(fieldNumber, kv =>
        {
            kv.WriteString(1, key);        // KeyValue.key
            kv.WriteMessage(2, anyVal =>   // KeyValue.value (AnyValue)
            {
                anyVal.WriteStringUnconditional(1, value); // AnyValue.string_value
            });
        });
    }
}
