// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
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
            "trace" => 1,
            "debug" => 5,
            "info" => 9,
            "notice" => 10,
            "metric" => 9,
            "success" => 9,
            "warn" => 13,
            "error" => 17,
            "critical" => 21,
            "security" => 17,
            "fatal" => 21,
            _ => Math.Clamp(levelRegistry.GetRank(level) * 4 + 1, 1, 24)
        };
    }
}
