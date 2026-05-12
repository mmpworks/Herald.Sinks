// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using MMP.Herald;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Services;
using SysEventLog = System.Diagnostics.EventLog;
using SysEntryType = System.Diagnostics.EventLogEntryType;

namespace Herald.Sinks.EventLog;

/// <summary>
/// Sink that writes Herald log events to the Windows Event Log.
/// Drop-in equivalent of Serilog.Sinks.EventLog — forwards level,
/// category, and message to <c>EventLog.WriteEntry</c> with the right
/// <see cref="SysEntryType"/> mapping.
/// </summary>
/// <remarks>
/// <para>
/// <b>Windows-only.</b> The underlying BCL type supports writing on
/// Windows only. On Linux / macOS the ctor throws
/// <see cref="PlatformNotSupportedException"/>. The class carries
/// <c>[SupportedOSPlatform("windows")]</c> so the compiler flags any
/// cross-platform use.
/// </para>
/// <para>
/// <b>Source registration.</b> Event sources must be registered on the
/// machine before writes succeed. Production deployments typically
/// register sources via installer (<c>EventLog.CreateEventSource</c>
/// requires admin). The sink's <c>autoCreateSource</c> flag will attempt
/// registration on first write for convenience — leave it off in
/// locked-down environments where admin rights are unavailable.
/// </para>
/// <para>
/// <b>Level mapping.</b> Trace / Debug / Info / Notice / Success →
/// Information. Warn → Warning. Error / Critical / Security →
/// Error. Unknown levels → Information. Matches Serilog's default
/// mapping so existing Event Log filters carry over.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class EventLogSink : ILogger
{
    private readonly string _source;
    private readonly string _logName;
    private readonly string _machineName;

    public EventLogSink(
        string source,
        string logName = "Application",
        string? machineName = null,
        bool autoCreateSource = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(logName);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException(
                "EventLogSink is Windows-only. The underlying BCL EventLog API is " +
                "unavailable on Linux and macOS. For a cross-platform system-log " +
                "sink use Herald.Sinks.Syslog instead.");
        }

        _source = source;
        _logName = logName;
        _machineName = machineName ?? ".";

        if (autoCreateSource && !SysEventLog.SourceExists(source, _machineName))
        {
            // Admin-only operation on Windows. Surface the failure with
            // the caller's context instead of letting a bare
            // SecurityException bubble — a missing source is the most
            // common deployment misconfiguration this sink sees.
            try
            {
                SysEventLog.CreateEventSource(new System.Diagnostics.EventSourceCreationData(source, logName)
                {
                    MachineName = _machineName
                });
            }
            catch (Exception ex) when (ex is not PlatformNotSupportedException)
            {
                throw new InvalidOperationException(
                    $"Failed to create Event Log source '{source}' on log '{logName}'. " +
                    "Source creation requires administrator privilege; register the " +
                    "source in your installer instead or run once as admin to create it.",
                    ex);
            }
        }
    }

    public void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        var entryType = MapEntryType(logEvent.Level);
        var message = BuildMessage(logEvent);

        // EventLog.WriteEntry caps message length at 31839 characters;
        // truncate rather than throw. Rare path — skip the check on the
        // hot side by truncating unconditionally when oversized.
        const int MaxEventLogMessageLength = 31_839;
        if (message.Length > MaxEventLogMessageLength)
        {
            message = message[..MaxEventLogMessageLength];
        }

        SysEventLog.WriteEntry(_source, message, entryType);
    }

    private static SysEntryType MapEntryType(LogLevel level)
    {
        // Use Key string comparison so custom levels with matching names
        // map too. Unknown levels fall through to Information —
        // conservative default that won't trigger Event Log's alert rules.
        return level.Key switch
        {
            "error" or "critical" or "security" => SysEntryType.Error,
            "warn" => SysEntryType.Warning,
            _ => SysEntryType.Information,
        };
    }

    private static string BuildMessage(LogEvent evt)
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
