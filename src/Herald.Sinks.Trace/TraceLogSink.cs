// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Globalization;
using MMP.Herald;
using MMP.Herald.Events;
using MMP.Herald.Services;
// Namespace `Herald.Sinks.Trace` shadows `System.Diagnostics.Trace`, so
// alias the BCL type to a non-conflicting name.
using SysTrace = System.Diagnostics.Trace;

namespace Herald.Sinks.Trace;

/// <summary>
/// Sink that writes log events to <see cref="System.Diagnostics.Trace"/>
/// listeners. Events are visible to any <c>TraceListener</c> registered
/// on <see cref="SysTrace.Listeners"/> — the Windows Event Log listener,
/// the TextWriterTraceListener, Azure Monitor's TraceListener, and every
/// other tool hooked into the Trace pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Differs from <c>Herald.Sinks.Debug</c> only in the BCL sink it forwards
/// to. <see cref="SysTrace.WriteLine(string?)"/> is
/// <c>[Conditional("TRACE")]</c> — the .NET SDK defines TRACE by default
/// in both Debug and Release, so no csproj gymnastics are needed. The
/// package declares TRACE explicitly anyway to document the dependency.
/// </para>
/// <para>
/// Format: <c>[HH:mm:ss.fff] [level] [category] message</c>, with
/// exception text appended when present. Minimal by design; wrap with
/// an output transformer chain for custom layouts.
/// </para>
/// </remarks>
public sealed class TraceLogSink : ILogger
{
    private readonly string? _category;

    /// <summary>
    /// Create a Trace sink. The <paramref name="category"/> argument is
    /// forwarded to <see cref="SysTrace.WriteLine(string, string)"/> so
    /// listeners that group by category can filter Herald output from
    /// other diagnostic chatter.
    /// </summary>
    public TraceLogSink(string? category = null)
    {
        _category = category;
    }

    public void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        var line = FormatLine(logEvent);

        if (_category is null)
            SysTrace.WriteLine(line);
        else
            SysTrace.WriteLine(line, _category);
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
