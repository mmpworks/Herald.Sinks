// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using MMP.Herald.Levels;

namespace Herald.Sinks.Seq;

/// <summary>
/// Maps Herald's <see cref="LogLevel"/> keys to Seq's CLEF
/// <c>@l</c> level strings. Seq expects the Serilog-style names
/// (<c>Verbose</c>, <c>Debug</c>, <c>Information</c>, <c>Warning</c>,
/// <c>Error</c>, <c>Fatal</c>); the <c>@l</c> field is omitted when the
/// level is the default <c>Information</c>, per CLEF's size-saving
/// convention.
/// </summary>
internal static class SeqLevelMapper
{
    /// <summary>
    /// Return the CLEF level string for the given Herald level, or
    /// <c>null</c> when the level is the CLEF default (Information) and
    /// the field should be omitted.
    /// </summary>
    public static string? MapLevel(LogLevel level)
    {
        return level.Key.ToLowerInvariant() switch
        {
            "trace" => "Verbose",
            "debug" => "Debug",
            "info" => null,          // CLEF default
            "notice" => null,
            "metric" => null,
            "success" => null,
            "warn" => "Warning",
            "error" => "Error",
            "security" => "Error",
            "critical" => "Fatal",
            "fatal" => "Fatal",
            _ => level.DisplayName
        };
    }
}
