// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Globalization;
using System.Text;
using MMP.Herald.Events;
using MMP.Herald.Levels;

namespace Herald.Sinks.Syslog;

/// <summary>
/// Formats a <see cref="LogEvent"/> as a syslog wire message per RFC
/// 3164 or RFC 5424. Split out from the sink proper so the formatting
/// is independently testable — the UDP / TCP transports exercise the
/// same byte output.
/// </summary>
internal static class SyslogMessageBuilder
{
    // Severity levels per RFC 5424 §6.2.1. Smaller is more severe.
    private const int SeverityEmergency = 0;
    private const int SeverityAlert = 1;
    private const int SeverityCritical = 2;
    private const int SeverityError = 3;
    private const int SeverityWarning = 4;
    private const int SeverityNotice = 5;
    private const int SeverityInformational = 6;
    private const int SeverityDebug = 7;

    public static string Build(
        LogEvent evt,
        SyslogFormat format,
        SyslogFacility facility,
        string host,
        string appName,
        string? processId)
    {
        var priority = ComputePriority(facility, evt.Level);
        var message = string.IsNullOrEmpty(evt.Message) ? evt.MessageTemplate : evt.Message;

        return format switch
        {
            SyslogFormat.Rfc5424 => BuildRfc5424(priority, evt.TimeUtc, host, appName, processId, message),
            SyslogFormat.Rfc3164 => BuildRfc3164(priority, evt.TimeUtc, host, appName, processId, message),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown syslog format."),
        };
    }

    private static int ComputePriority(SyslogFacility facility, LogLevel level)
    {
        var severity = MapSeverity(level);
        return (int)facility * 8 + severity;
    }

    private static int MapSeverity(LogLevel level) => level.Key switch
    {
        "trace" or "debug" => SeverityDebug,
        "info" => SeverityInformational,
        "notice" or "success" => SeverityNotice,
        "warn" => SeverityWarning,
        "error" => SeverityError,
        "critical" => SeverityCritical,
        "security" => SeverityAlert,
        _ => SeverityInformational,
    };

    // RFC 5424 format:
    //   <PRI>1 TIMESTAMP HOST APPNAME PROCID MSGID STRUCTURED-DATA MSG
    // We emit MSGID / STRUCTURED-DATA as NILVALUE ("-") since Herald's
    // event model does not mirror those fields cleanly.
    private static string BuildRfc5424(
        int priority,
        DateTimeOffset timeUtc,
        string host,
        string appName,
        string? processId,
        string message)
    {
        var timestamp = timeUtc.ToString("yyyy-MM-ddTHH:mm:ss.ffffffK", CultureInfo.InvariantCulture);
        var procId = string.IsNullOrWhiteSpace(processId) ? "-" : processId;

        var sb = new StringBuilder(128 + message.Length);
        sb.Append('<').Append(priority).Append('>').Append('1').Append(' ');
        sb.Append(timestamp).Append(' ');
        sb.Append(Sanitize(host)).Append(' ');
        sb.Append(Sanitize(appName)).Append(' ');
        sb.Append(Sanitize(procId)).Append(' ');
        sb.Append("- ");     // MSGID
        sb.Append("- ");     // STRUCTURED-DATA (NILVALUE)
        sb.Append(message);
        return sb.ToString();
    }

    // RFC 3164 format:
    //   <PRI>MMM dd HH:mm:ss HOST APPNAME[PID]: MSG
    // Traditional BSD-style timestamp — month abbreviation, day (space-
    // padded), and 24h time. No year, no timezone, sub-second precision
    // unavailable. Many older collectors still prefer this format.
    private static string BuildRfc3164(
        int priority,
        DateTimeOffset timeUtc,
        string host,
        string appName,
        string? processId,
        string message)
    {
        var timestamp = timeUtc.ToString("MMM ", CultureInfo.InvariantCulture) +
                        timeUtc.Day.ToString("D", CultureInfo.InvariantCulture).PadLeft(2, ' ') + ' ' +
                        timeUtc.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        var sb = new StringBuilder(64 + message.Length);
        sb.Append('<').Append(priority).Append('>');
        sb.Append(timestamp).Append(' ');
        sb.Append(Sanitize(host)).Append(' ');
        sb.Append(Sanitize(appName));
        if (!string.IsNullOrWhiteSpace(processId))
        {
            sb.Append('[').Append(Sanitize(processId)).Append(']');
        }
        sb.Append(": ").Append(message);
        return sb.ToString();
    }

    // Replace whitespace and control characters that would break the
    // syslog header fields. The HOST and APPNAME fields are required to
    // contain no spaces; swap with dashes instead of dropping so the
    // operator can still correlate.
    private static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value)) return "-";
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsWhiteSpace(chars[i]) || char.IsControl(chars[i]))
                chars[i] = '-';
        }
        return new string(chars);
    }
}
