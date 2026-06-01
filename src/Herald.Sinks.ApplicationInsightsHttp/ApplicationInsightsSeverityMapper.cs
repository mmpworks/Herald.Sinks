// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using MMP.Herald.Levels;

namespace Herald.Sinks.ApplicationInsightsHttp;

/// <summary>
/// Maps Herald's <see cref="LogLevel"/> to Application Insights'
/// numeric <c>SeverityLevel</c> used on the wire (0..4). Matches the
/// shape of <see cref="OtlpSinks.OtlpSeverityMapper"/> — switch on the
/// level's key string so custom levels registered in the registry
/// still map sensibly.
/// </summary>
/// <remarks>
/// AI scale (SDK docs):
///   0 = Verbose
///   1 = Information
///   2 = Warning
///   3 = Error
///   4 = Critical
/// Unknown / custom levels fall through to Information — the conservative
/// choice so a custom "metric" or "audit" level appears in AI at the
/// normal trace volume rather than silently as Verbose or flagged as Error.
/// </remarks>
internal static class ApplicationInsightsSeverityMapper
{
    public const int Verbose = 0;
    public const int Information = 1;
    public const int Warning = 2;
    public const int Error = 3;
    public const int Critical = 4;

    public static int MapSeverityLevel(LogLevel level)
    {
        return level.Key.ToLowerInvariant() switch
        {
            "verbose" => Verbose,
            "debug" => Verbose,
            "information" => Information,
            "notice" => Information,
            "metric" => Information,
            "success" => Information,
            "warning" => Warning,
            "error" => Error,
            "security" => Error,
            "fatal" => Critical,
            _ => Information
        };
    }
}
