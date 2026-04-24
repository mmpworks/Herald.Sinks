// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using MMP.Herald.Levels;

namespace Herald.Sinks.Splunk;

/// <summary>
/// Maps Herald's <see cref="LogLevel"/> keys to Splunk's conventional
/// severity strings used inside a HEC <c>event.severity</c> field.
/// Splunk treats these as free-form text, so the choice is about
/// matching existing search queries rather than satisfying a schema.
/// </summary>
internal static class SplunkLevelMapper
{
    public static string MapLevel(LogLevel level)
    {
        return level.Key.ToLowerInvariant() switch
        {
            "trace" => "TRACE",
            "debug" => "DEBUG",
            "info" => "INFO",
            "notice" => "INFO",
            "metric" => "INFO",
            "success" => "INFO",
            "warn" => "WARN",
            "error" => "ERROR",
            "security" => "ERROR",
            "critical" => "FATAL",
            "fatal" => "FATAL",
            _ => level.DisplayName.ToUpperInvariant()
        };
    }
}
