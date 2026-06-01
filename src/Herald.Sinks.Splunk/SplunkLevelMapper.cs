// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
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
            "verbose" => "TRACE",
            "debug" => "DEBUG",
            "information" => "INFO",
            "notice" => "INFO",
            "metric" => "INFO",
            "success" => "INFO",
            "warning" => "WARN",
            "error" => "ERROR",
            "security" => "ERROR",
            "fatal" => "FATAL",
            _ => level.DisplayName.ToUpperInvariant()
        };
    }
}
