// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using MMP.Herald.Levels;

namespace Herald.Sinks.Otlp;

/// <summary>
/// Maps Herald log levels to OTLP severity numbers per the OpenTelemetry specification.
/// Shared by OtlpJsonLogSink and OtlpProtobufLogSerializer to eliminate duplication.
/// TRACE=1-4, DEBUG=5-8, INFO=9-12, WARN=13-16, ERROR=17-20, FATAL=21-24.
/// </summary>
internal static class OtlpSeverityMapper
{
    public static int MapSeverityNumber(LogLevel level, ILogLevelRegistry levelRegistry) {
        return level.Key.ToLowerInvariant() switch
        {
            "verbose" => 1,
            "debug" => 5,
            "information" => 9,
            "notice" => 10,
            "metric" => 9,
            "success" => 9,
            "warning" => 13,
            "error" => 17,
            "security" => 17,
            "fatal" => 21,
            _ => Math.Clamp(levelRegistry.GetRank(level) * 4 + 1, 1, 24)
        };
    }
}
