// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Globalization;
using System.IO;
using MMP.Herald;
using MMP.Herald.Events;
using MMP.Herald.Services;

namespace Herald.Sinks.TextWriter;

/// <summary>
/// Sink that writes formatted log lines to any
/// <see cref="System.IO.TextWriter"/> supplied by the caller. Useful for
/// tests that want to capture output into a <c>StringWriter</c>, for
/// redirecting to a memory stream, and for custom writer shims.
/// </summary>
/// <remarks>
/// <para>
/// Ownership is explicit: pass <c>disposeWriter: true</c> if the sink
/// should dispose the writer on <see cref="Dispose"/>, or leave the
/// default <c>false</c> to keep the writer alive for the caller.
/// </para>
/// <para>
/// Format: <c>[HH:mm:ss.fff] [level] [category] message</c>, exception
/// text appended on a following line when present. The writer is
/// flushed after every event so tests see output immediately.
/// </para>
/// </remarks>
public sealed class TextWriterLogSink : ILogger, IDisposable
{
    private readonly System.IO.TextWriter _writer;
    private readonly bool _disposeWriter;
    private readonly object _syncRoot = new();

    /// <summary>
    /// Create a sink that writes to <paramref name="writer"/>. Set
    /// <paramref name="disposeWriter"/> to <c>true</c> when the sink
    /// should own the writer's lifetime.
    /// </summary>
    public TextWriterLogSink(System.IO.TextWriter writer, bool disposeWriter = false)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
        _disposeWriter = disposeWriter;
    }

    public void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        var line = FormatLine(logEvent);

        // TextWriter isn't thread-safe for concurrent writes — lock around
        // Write+Flush so two Log calls from different threads don't
        // interleave half-lines. Cost is a single monitor acquire per event.
        lock (_syncRoot)
        {
            _writer.WriteLine(line);
            _writer.Flush();
        }
    }

    public void Dispose()
    {
        if (_disposeWriter)
            _writer.Dispose();
    }

    private static string FormatLine(LogEvent evt)
    {
        var time = evt.TimeUtc.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var level = evt.Level.Key;
        var category = evt.Category.Value;
        var message = string.IsNullOrEmpty(evt.Message) ? evt.MessageTemplate : evt.Message;

        var line = $"[{time}] [{level}] [{category}] {message}";

        if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value) && value is Exception ex)
        {
            line += Environment.NewLine + ex;
        }

        return line;
    }
}
