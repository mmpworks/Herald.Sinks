// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using MMP.Herald.Events;
using MMP.Herald.Services;

namespace Herald.Sinks.Graylog;

/// <summary>
/// Formats Herald log events as GELF 1.1 JSON messages. GELF maps
/// fields:
/// <list type="bullet">
///   <item><c>version</c> — "1.1"</item>
///   <item><c>host</c> — the source hostname</item>
///   <item><c>short_message</c> — required, rendered message</item>
///   <item><c>full_message</c> — optional, full exception text</item>
///   <item><c>timestamp</c> — unix seconds (float)</item>
///   <item><c>level</c> — syslog severity 0-7</item>
///   <item><c>_*</c> — custom fields (must start with underscore)</item>
/// </list>
/// </summary>
internal static class GraylogMessageBuilder
{
    public static string Build(LogEvent evt, string host)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("version", "1.1");
            writer.WriteString("host", host);
            writer.WriteString("short_message", string.IsNullOrEmpty(evt.Message) ? evt.MessageTemplate : evt.Message);

            var unixSeconds = evt.TimeUtc.ToUnixTimeMilliseconds() / 1000.0;
            writer.WriteNumber("timestamp", unixSeconds);
            writer.WriteNumber("level", MapSyslogSeverity(evt.Level.Key));

            if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value) && value is Exception ex)
            {
                writer.WriteString("full_message", ex.ToString());
                writer.WriteString("_exception_type", ex.GetType().FullName ?? ex.GetType().Name);
            }

            writer.WriteString("_category", evt.Category.Value);
            writer.WriteString("_template", evt.MessageTemplate ?? string.Empty);

            if (evt.Properties is not null && evt.Properties.Count > 0)
            {
                foreach (var prop in evt.Properties)
                {
                    // GELF requires custom fields to start with underscore.
                    // Prefix user property names so Graylog accepts them.
                    writer.WriteString("_" + prop.Name, prop.ResolvedValue?.ToString());
                }
            }

            writer.WriteEndObject();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    // Maps Herald level → syslog severity 0-7 per GELF spec.
    private static int MapSyslogSeverity(string levelKey) => levelKey switch
    {
        "trace" or "debug" => 7,
        "info" => 6,
        "notice" or "success" => 5,
        "warn" => 4,
        "error" => 3,
        "critical" => 2,
        "security" => 1,
        _ => 6,
    };
}
