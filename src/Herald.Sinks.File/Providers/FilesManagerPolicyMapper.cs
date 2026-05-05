// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using MMP.Herald.Configuration.Runtime;
using MMPWorks.RollingFiles;

namespace Herald.Sinks.File.Providers;

/// <summary>
/// Translates the v2 sink-property bag (mmpform-driven) into a
/// <see cref="FilesManagerPolicy"/>.
///
/// <para>
/// The bag's keys mirror <c>configuration-text_file.mmpform</c> /
/// <c>configuration-json_file.mmpform</c>. Anything missing falls
/// through to the policy record's own defaults — those are sensible
/// for a "single file forever" sink, which matches the mmpform's
/// default state where <c>rollingLogsEnabled = false</c>.
/// </para>
///
/// <para>
/// The mapper carries every documented mmpform key plus the new keys
/// that <see cref="FilesManagerPolicy"/> exposes
/// (<c>compression</c>, <c>diskFull</c>, <c>cleanRollOnStartup</c>,
/// <c>pathTokens</c>) so a future mmpform that lights those fields
/// up reaches the policy automatically. The keys default to "off"
/// when absent, which preserves today's behaviour.
/// </para>
///
/// <para>
/// Stays internal: the bag's interpretation is owned by this package
/// — Core never reaches into the dictionary. Mirrors the boundary
/// the legacy <see cref="FileSinkRuntimeConfig"/> helper kept.
/// </para>
/// </summary>
internal static class FilesManagerPolicyMapper
{
    // ── Property keys (mirrors the mmpform __properties block) ────
    private const string KeyDirectory          = "logDirectory";
    private const string KeyTemplate           = "logFileTemplate";
    private const string KeyExtension          = "logExtension";
    private const string KeyActiveFileName     = "activeFileName";
    private const string KeyRollingEnabled     = "rollingLogsEnabled";
    private const string KeyRollingInterval    = "rollingInterval";
    private const string KeyMaxFileSize        = "maxFileSize";
    private const string KeyMaxMessages        = "maxMessagesPerFile";
    private const string KeyMaxRetainedFiles   = "maxRetainedFiles";
    private const string KeyTotalSizeCap       = "totalSizeCap";
    private const string KeyRetentionDays      = "retentionDays";
    private const string KeyNamePattern        = "namePattern";
    private const string KeyNamePatternLegacy  = "fileNamePattern"; // json_file's older spelling
    private const string KeyStartMinute        = "startMinute";
    private const string KeyCaptureDurationMin = "captureDurationMinutes";
    private const string KeyCompression        = "compression";
    private const string KeyDiskFull           = "diskFull";
    private const string KeyCleanRollOnStartup = "cleanRollOnStartup";
    private const string KeyShareForWriting    = "shareForWriting";
    private const string KeyPathTokens         = "pathTokens";
    private const string KeyLocale             = "locale";

    /// <summary>
    /// Build a <see cref="FilesManagerPolicy"/> from the sink
    /// definition's property bag. Throws when the bag is missing
    /// required keys (<see cref="KeyDirectory"/>,
    /// <see cref="KeyTemplate"/>) — they are also required by the
    /// mmpform contract.
    /// </summary>
    public static FilesManagerPolicy From(LoggingRuntimeSinkDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var bag = definition.Properties
            ?? throw new InvalidOperationException(
                $"Sink '{definition.Name}' has no Properties bag — cannot build a FilesManagerPolicy.");

        var directory = ReadString(bag, KeyDirectory)
            ?? throw new InvalidOperationException(
                $"Sink '{definition.Name}' is missing the required '{KeyDirectory}' property.");
        var template = ReadString(bag, KeyTemplate)
            ?? throw new InvalidOperationException(
                $"Sink '{definition.Name}' is missing the required '{KeyTemplate}' property.");

        // Roll-trigger fields only flow through when rolling is on.
        // When the mmpform's rollingLogsEnabled toggle is off, we hand
        // FilesManager an interval/size/count of None so it produces
        // the "single file forever" shape — same as the legacy
        // RollingFileLineWriter behaviour for that toggle state.
        var rollingOn = ReadBool(bag, KeyRollingEnabled) == true;

        return new FilesManagerPolicy(
            Directory: directory,
            FileNameTemplate: template,
            Extension: NormaliseExtension(ReadString(bag, KeyExtension)),
            ActiveFileName: ReadString(bag, KeyActiveFileName),
            Interval: rollingOn ? ParseInterval(ReadString(bag, KeyRollingInterval)) : RollingInterval.None,
            MaxBytesPerFile: rollingOn ? ParseHumanByteSize(ReadString(bag, KeyMaxFileSize)) : null,
            MaxMessagesPerFile: rollingOn ? ReadLong(bag, KeyMaxMessages) : null,
            MaxRetainedFiles: ReadInt(bag, KeyMaxRetainedFiles),
            TotalSizeCapBytes: ParseHumanByteSize(ReadString(bag, KeyTotalSizeCap)),
            RetentionDays: ReadInt(bag, KeyRetentionDays),
            Compression: ParseCompression(ReadString(bag, KeyCompression)),
            DiskFull: ParseDiskFull(ReadString(bag, KeyDiskFull)),
            StartMinute: ReadInt(bag, KeyStartMinute) ?? 0,
            CaptureDurationMinutes: ReadInt(bag, KeyCaptureDurationMin) ?? 60,
            FileNameSuffix: PreferredString(bag, KeyNamePattern, KeyNamePatternLegacy),
            Locale: ReadString(bag, KeyLocale),
            ShareForWriting: ReadBool(bag, KeyShareForWriting) == true,
            // Carried through to the policy; the runtime support for
            // CleanRollOnStartup lands in a later FilesManager phase.
            // Keeping it on the policy now means a future runtime
            // upgrade picks it up without a mapper change.
            CleanRollOnStartup: ReadBool(bag, KeyCleanRollOnStartup) == true,
            // Token values are taken as-is. PathTokens validation
            // (rejecting "/" / ".." / etc.) lives in
            // FilesManagerPolicy / FilesManager at expansion time —
            // the mapper does not duplicate it because a single
            // rejection point keeps the contract auditable.
            PathTokens: ReadStringMap(bag, KeyPathTokens));
    }

    // ── Bag readers (tolerant; null / wrong-type → fall through) ──

    private static string? ReadString(IReadOnlyDictionary<string, object?> bag, string key) =>
        bag.TryGetValue(key, out var raw) && raw is not null ? raw.ToString() : null;

    private static string? PreferredString(
        IReadOnlyDictionary<string, object?> bag, string primaryKey, string legacyKey)
    {
        // Empty pattern means "use the writer's per-interval default" —
        // matches the mmpform's "blank = …" help text. Coerce empty to
        // null so the policy hits the default branch instead of an
        // empty suffix.
        var primary = ReadString(bag, primaryKey);
        if (!string.IsNullOrEmpty(primary)) return primary;
        var legacy = ReadString(bag, legacyKey);
        return string.IsNullOrEmpty(legacy) ? null : legacy;
    }

    private static bool? ReadBool(IReadOnlyDictionary<string, object?> bag, string key)
    {
        if (!bag.TryGetValue(key, out var raw) || raw is null) return null;
        if (raw is bool b) return b;
        return bool.TryParse(raw.ToString(), out var parsed) ? parsed : (bool?)null;
    }

    private static long? ReadLong(IReadOnlyDictionary<string, object?> bag, string key)
    {
        if (!bag.TryGetValue(key, out var raw) || raw is null) return null;
        // Cognitive Complexity note: switch covers the JSON
        // deserialiser's three numeric carriers (long / int / double)
        // plus the string fallback. One pass, one return per arm.
        // Doubles are truncated toward zero — `30.7` becomes 30 — so
        // a fractional JSON literal lands as the integer floor rather
        // than null. That mirrors the legacy FileSinkRuntimeConfig
        // shape and matches operator intent ("about 30 files").
        return raw switch
        {
            long l   => l,
            int i    => i,
            double d => (long)d,
            string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
            _ => (long?)null
        };
    }

    private static int? ReadInt(IReadOnlyDictionary<string, object?> bag, string key)
    {
        var l = ReadLong(bag, key);
        return l is null ? null : (int?)Math.Min(int.MaxValue, Math.Max(int.MinValue, l.Value));
    }

    private static IReadOnlyDictionary<string, string>? ReadStringMap(
        IReadOnlyDictionary<string, object?> bag, string key)
    {
        if (!bag.TryGetValue(key, out var raw) || raw is null) return null;
        if (raw is not IReadOnlyDictionary<string, object?> nested) return null;
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in nested)
        {
            if (kv.Value is null) continue;
            result[kv.Key] = kv.Value.ToString() ?? string.Empty;
        }
        return result.Count == 0 ? null : result;
    }

    // ── Enum parsers ─────────────────────────────────────────────

    private static RollingInterval ParseInterval(string? raw) => (raw ?? "").ToLowerInvariant() switch
    {
        "hourly" => RollingInterval.Hourly,
        "daily"  => RollingInterval.Daily,
        "custom" => RollingInterval.Custom,
        _        => RollingInterval.None,
    };

    private static CompressionMode ParseCompression(string? raw) => (raw ?? "").ToLowerInvariant() switch
    {
        "gzip"   => CompressionMode.Gzip,
        "brotli" => CompressionMode.Brotli,
        _        => CompressionMode.None,
    };

    private static DiskFullStrategy ParseDiskFull(string? raw) => (raw ?? "").ToLowerInvariant() switch
    {
        "drop"     => DiskFullStrategy.Drop,
        "fallback" => DiskFullStrategy.Fallback,
        _          => DiskFullStrategy.Stop,
    };

    // ── Format helpers ───────────────────────────────────────────

    // The mmpform stores the extension without a leading dot
    // ("log", "ndjson"); FilesManagerPolicy expects ".log" / ".ndjson".
    // Either form is accepted on the way in.
    private static string NormaliseExtension(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return ".log";
        return raw.StartsWith('.') ? raw : "." + raw;
    }

    // Parse `10MB`, `1GB`, `500KB`, `0`, `""`. Returns null when the
    // input is empty or unparsable so the policy treats it as "no
    // cap" rather than failing the build.
    private static long? ParseHumanByteSize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        long multiplier = 1;
        if (s.EndsWith("GB", StringComparison.OrdinalIgnoreCase)) { multiplier = 1_073_741_824L; s = s[..^2]; }
        else if (s.EndsWith("MB", StringComparison.OrdinalIgnoreCase)) { multiplier = 1_048_576L;     s = s[..^2]; }
        else if (s.EndsWith("KB", StringComparison.OrdinalIgnoreCase)) { multiplier = 1_024L;          s = s[..^2]; }
        else if (s.EndsWith("B",  StringComparison.OrdinalIgnoreCase)) { multiplier = 1L;              s = s[..^1]; }

        if (!double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return null;
        if (n <= 0) return null;
        return (long)Math.Round(n * multiplier);
    }
}
