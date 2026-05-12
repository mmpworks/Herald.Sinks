// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using MMP.Herald;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Pipeline;

namespace Herald.Sinks.Otlp;

/// <summary>
/// Sink that writes log events as length-delimited OTLP protobuf records to a .pb file.
/// Each record is a 4-byte little-endian length prefix followed by the protobuf bytes.
/// Supports optional rolling by file size.
///
/// The file can be read back by reading 4 bytes (length), then reading that many bytes
/// (OTLP ExportLogsServiceRequest), and repeating until EOF.
/// </summary>
public sealed class ProtobufFileLogSink : ILogger, IBatchedLogSink, IDisposable
{
    private readonly string _basePath;
    private readonly long _maxFileSizeBytes;
    private readonly OtlpProtobufLogSerializer _serializer;
    private readonly object _lock = new();
    private FileStream? _stream;
    private long _currentFileSize;
    private int _rollIndex;

    /// <summary>
    /// Create a protobuf file sink.
    /// </summary>
    /// <param name="path">Output file path (real filesystem path, not a virtual path).</param>
    /// <param name="levelRegistry">Level registry for severity mapping.</param>
    /// <param name="resourceAttributes">Optional OTLP resource attributes.</param>
    /// <param name="maxFileSizeBytes">Roll to a new file after this size. 0 = no rolling.</param>
    public ProtobufFileLogSink(
        string path,
        ILogLevelRegistry levelRegistry,
        IReadOnlyDictionary<string, string>? resourceAttributes = null,
        long maxFileSizeBytes = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(levelRegistry);

        _basePath = path;
        _maxFileSizeBytes = maxFileSizeBytes;
        _serializer = new OtlpProtobufLogSerializer(levelRegistry, resourceAttributes);

        EnsureDirectoryExists(path);
        _stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        _currentFileSize = _stream.Length;
    }

    public void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        WriteRecord(_serializer.Serialize(logEvent));
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        // Each event is a separate length-delimited record for streaming reads
        foreach (var logEvent in events)
        {
            WriteRecord(_serializer.Serialize(logEvent));
        }
    }

    private void WriteRecord(byte[] payload)
    {
        lock (_lock)
        {
            RollIfNeeded(payload.Length);

            if (_stream is null) return;

            // Write 4-byte little-endian length prefix
            var lengthBytes = BitConverter.GetBytes(payload.Length);
            if (!BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);

            _stream.Write(lengthBytes, 0, 4);
            _stream.Write(payload, 0, payload.Length);
            _stream.Flush();

            _currentFileSize += 4 + payload.Length;
        }
    }

    private void RollIfNeeded(int incomingSize)
    {
        if (_maxFileSizeBytes <= 0) return;
        if (_currentFileSize + 4 + incomingSize <= _maxFileSizeBytes) return;

        _stream?.Dispose();
        _rollIndex++;

        var dir = Path.GetDirectoryName(_basePath) ?? ".";
        var name = Path.GetFileNameWithoutExtension(_basePath);
        var ext = Path.GetExtension(_basePath);
        var rolledPath = Path.Combine(dir, $"{name}_{_rollIndex:D4}{ext}");

        _stream = new FileStream(rolledPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        _currentFileSize = 0;
    }

    private static void EnsureDirectoryExists(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _stream?.Dispose();
            _stream = null;
        }
    }
}
