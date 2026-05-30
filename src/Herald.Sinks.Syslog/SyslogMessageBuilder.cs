// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
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

    // Legacy 6-arg overload preserved for callers (including the
    // existing test suite) that pre-date the structured-data wiring.
    // Forwards to the full overload with SD emission on and the
    // default SD-ID. Existing behaviour for events without properties
    // is unchanged — STRUCTURED-DATA stays NILVALUE there either way.
    public static string Build(
        LogEvent evt,
        SyslogFormat format,
        SyslogFacility facility,
        string host,
        string appName,
        string? processId)
        => Build(evt, format, facility, host, appName, processId,
                 structuredDataId: null, structuredDataEnabled: true);

    public static string Build(
        LogEvent evt,
        SyslogFormat format,
        SyslogFacility facility,
        string host,
        string appName,
        string? processId,
        string? structuredDataId,
        bool structuredDataEnabled)
    {
        var priority = ComputePriority(facility, evt.Level);
        var message = string.IsNullOrEmpty(evt.Message) ? evt.MessageTemplate : evt.Message;

        return format switch
        {
            SyslogFormat.Rfc5424 => BuildRfc5424(
                priority, evt, host, appName, processId, message,
                structuredDataId, structuredDataEnabled),
            SyslogFormat.Rfc3164 => BuildRfc3164(
                priority, evt.TimeUtc, host, appName, processId, message),
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
    // STRUCTURED-DATA carries Herald properties when they are present and
    // SD emission is enabled — one SD-ELEMENT keyed by the operator's
    // SD-ID (default `herald@32473`, the IANA example enterprise number).
    // When the event has no properties or SD is disabled, the field is
    // NILVALUE ("-"). The MSGID field is still NILVALUE — Herald doesn't
    // have a stable cross-event id to put there.
    private static string BuildRfc5424(
        int priority,
        LogEvent evt,
        string host,
        string appName,
        string? processId,
        string message,
        string? structuredDataId,
        bool structuredDataEnabled)
    {
        var timestamp = evt.TimeUtc.ToString("yyyy-MM-ddTHH:mm:ss.ffffffK", CultureInfo.InvariantCulture);
        var procId = string.IsNullOrWhiteSpace(processId) ? "-" : processId;

        var sb = new StringBuilder(192 + message.Length);
        sb.Append('<').Append(priority).Append('>').Append('1').Append(' ');
        sb.Append(timestamp).Append(' ');
        sb.Append(Sanitize(host)).Append(' ');
        sb.Append(Sanitize(appName)).Append(' ');
        sb.Append(Sanitize(procId)).Append(' ');
        sb.Append("- ");     // MSGID
        AppendStructuredData(sb, evt, structuredDataId, structuredDataEnabled);
        sb.Append(' ');
        sb.Append(message);
        return sb.ToString();
    }

    // STRUCTURED-DATA per RFC 5424 §6.3 is one or more SD-ELEMENTs:
    //   [SD-ID name1="value1" name2="value2"]
    // SD-ID's "@" form is reserved for enterprise-private IDs; the IANA
    // example enterprise number 32473 lets the default work in any
    // collector without registration. PARAM-VALUE escaping: `"` → `\"`,
    // `\` → `\\`, `]` → `\]`.
    private static void AppendStructuredData(
        StringBuilder sb,
        LogEvent evt,
        string? structuredDataId,
        bool structuredDataEnabled)
    {
        if (!structuredDataEnabled)
        {
            sb.Append('-');
            return;
        }

        var hasProperties = evt.Properties.Count > 0;
        if (!hasProperties)
        {
            sb.Append('-');
            return;
        }

        var sdId = string.IsNullOrWhiteSpace(structuredDataId)
            ? "herald@32473"
            : structuredDataId;

        sb.Append('[').Append(SanitizeSdId(sdId));
        foreach (var property in evt.Properties)
        {
            // RFC 5424 §6.3.3: PARAM-NAME is restricted to printable
            // ASCII except space, '=', ']', '"'. Reuse the sanitiser
            // so a property named "a=b" doesn't break the SD frame.
            var name = SanitizeParamName(property.Name);
            if (name.Length == 0) continue;

            var value = property.ResolvedValue?.ToString() ?? string.Empty;
            sb.Append(' ').Append(name).Append("=\"").Append(EscapeParamValue(value)).Append('"');
        }
        sb.Append(']');
    }

    // PARAM-VALUE escaping per RFC 5424 §6.3.3.
    private static string EscapeParamValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        // Fast path: nothing to escape.
        var needsEscape = false;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '"' || c == '\\' || c == ']') { needsEscape = true; break; }
        }
        if (!needsEscape) return value;

        var sb = new StringBuilder(value.Length + 8);
        foreach (var c in value)
        {
            if (c == '"' || c == '\\' || c == ']') sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }

    // SD-ID allows IETF-defined ids (alphanumeric, no '@') or
    // private-enterprise ids of the form "name@enterprise-number". The
    // sanitiser drops disallowed characters; an empty result falls back
    // to the default. RFC limits SD-ID to 32 characters.
    private static string SanitizeSdId(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            if (c == '"' || c == ']' || c == '=' || char.IsWhiteSpace(c) || char.IsControl(c)) continue;
            sb.Append(c);
            if (sb.Length >= 32) break;
        }
        return sb.Length == 0 ? "herald@32473" : sb.ToString();
    }

    private static string SanitizeParamName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        var sb = new StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            if (c == '"' || c == ']' || c == '=' || char.IsWhiteSpace(c) || char.IsControl(c)) continue;
            sb.Append(c);
            if (sb.Length >= 32) break; // RFC 5424 §6.3.3 caps PARAM-NAME at 32.
        }
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
