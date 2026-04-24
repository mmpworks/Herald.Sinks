// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
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
    /// Write a KeyValue attribute pair.
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
                anyVal.WriteString(1, value); // AnyValue.string_value
            });
        });
    }
}
