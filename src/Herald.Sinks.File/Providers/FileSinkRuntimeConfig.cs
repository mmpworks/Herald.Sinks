// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Output.Writers;

namespace Herald.Sinks.File.Providers;

/// <summary>
/// Build-time helper: collapses a sink definition's v2 property bag
/// (or, for un-migrated callers, its legacy <c>Path</c> + <c>RollingPolicy</c>
/// fields) into the concrete values <see cref="LogSinkFileWriterFactory"/>
/// needs to construct a writer. Stays internal so the bag's interpretation
/// is owned by the file-sink package — Core never reaches into the
/// dictionary.
///
/// <para>The bag's keys mirror <c>configuration-text_file.mmpform</c> /
/// <c>configuration-json_file.mmpform</c>. Anything missing falls
/// through to a code-level default consistent with the mmpform
/// <c>__properties</c> defaults; the contract layer above
/// (QuickLogBuilder, the management API) fills the bag with mmpform
/// defaults before the runtime ever sees it, so the code-level
/// fallbacks here only matter for hand-built definitions in tests or
/// custom hosts.</para>
/// </summary>
internal static class FileSinkRuntimeConfig
{
    public readonly record struct Resolved(
        string Path,
        LoggingRuntimeFileRollingPolicy? Rolling);

    public static Resolved From(LoggingRuntimeSinkDefinition definition)
    {
        // Legacy path: definition wired through the typed Path /
        // RollingPolicy fields. Until every file-sink call site moves
        // to the property bag, this stays the source of truth when
        // Properties is null.
        if (definition.Properties is null || definition.Properties.Count == 0)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(definition.Path);
            return new Resolved(definition.Path, definition.RollingPolicy);
        }

        var bag = definition.Properties;
        var directory = ReadString(bag, "logDirectory") ?? "logs";
        var template  = ReadString(bag, "logFileTemplate")
                        ?? throw new InvalidOperationException(
                            $"Sink '{definition.Name}' is missing the required 'logFileTemplate' property.");
        var extension = ReadString(bag, "logExtension") ?? "log";
        var path = JoinPath(directory, template, extension);

        var rolling = ReadBool(bag, "rollingLogsEnabled") == true
            ? BuildRollingPolicy(bag, definition.RollingPolicy)
            : null;

        return new Resolved(path, rolling);
    }

    // ── Bag readers (tolerant; fall back when the type doesn't match) ─

    private static string? ReadString(IReadOnlyDictionary<string, object?> bag, string key) =>
        bag.TryGetValue(key, out var raw) ? raw?.ToString() : null;

    private static bool? ReadBool(IReadOnlyDictionary<string, object?> bag, string key)
    {
        if (!bag.TryGetValue(key, out var raw) || raw is null) return null;
        if (raw is bool b) return b;
        return bool.TryParse(raw.ToString(), out var parsed) ? parsed : (bool?)null;
    }

    private static long? ReadLong(IReadOnlyDictionary<string, object?> bag, string key)
    {
        if (!bag.TryGetValue(key, out var raw) || raw is null) return null;
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

    private static double? ReadDouble(IReadOnlyDictionary<string, object?> bag, string key)
    {
        if (!bag.TryGetValue(key, out var raw) || raw is null) return null;
        return raw switch
        {
            double d => d,
            long l   => l,
            int i    => i,
            string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) => n,
            _ => (double?)null
        };
    }

    // Folder + base + extension into a path. Skips the dot when the
    // operator already wrote one, joins with forward slash so the
    // shape is the same on Linux and Windows (the OS-specific
    // separators come back at file-open time inside the writers).
    private static string JoinPath(string directory, string template, string extension)
    {
        var dir = directory.Replace('\\', '/').TrimEnd('/');
        var ext = extension.TrimStart('.');
        return string.IsNullOrEmpty(dir)
            ? $"{template}.{ext}"
            : $"{dir}/{template}.{ext}";
    }

    // Read the rolling-related keys out of the bag. Mirrors
    // DefaultLoggingConfigurationMapper.MapRollingPolicy but sources
    // the inputs from the v2 property names instead of the legacy
    // JsonFileRollingConfig record.
    //
    // <paramref name="legacy"/> carries fields the v2 mmpform does not
    // expose yet (logQueueSize, custom-interval startMinute /
    // captureDurationMinutes, locale). Bag values win for everything
    // the contract names; legacy fills the gap for fields that have
    // no v2 binding so existing fluent-API calls keep working during
    // the migration.
    private static LoggingRuntimeFileRollingPolicy BuildRollingPolicy(
        IReadOnlyDictionary<string, object?> bag,
        LoggingRuntimeFileRollingPolicy? legacy)
    {
        var intervalRaw = (ReadString(bag, "rollingInterval") ?? "daily").ToLowerInvariant();
        var interval = intervalRaw switch
        {
            "hourly" => LogFileRollingInterval.Hourly,
            "daily"  => LogFileRollingInterval.Daily,
            "custom" => LogFileRollingInterval.Custom,
            _        => LogFileRollingInterval.None,
        };

        var maxBytes = ParseHumanByteSize(ReadString(bag, "maxFileSize"));
        var totalCap = ParseHumanByteSize(ReadString(bag, "totalSizeCap"));
        var retentionDays = ReadInt(bag, "retentionDays")
                            ?? (ReadDouble(bag, "retentionDays") is { } rd ? (int)Math.Round(rd) : (int?)null);

        // Empty pattern means "use the writer's per-interval built-in
        // default" — matches the mmpform's "blank = …" help text.
        // Coerce to null so the writer hits that branch instead of
        // formatting with an empty suffix. text_file's mmpform names
        // the binding `namePattern`; json_file's still uses
        // `fileNamePattern`. Read either spelling so the same runtime
        // helper works for both forms.
        var pattern = ReadString(bag, "namePattern") ?? ReadString(bag, "fileNamePattern");
        if (string.IsNullOrEmpty(pattern)) pattern = null;

        return new LoggingRuntimeFileRollingPolicy(
            Interval: interval,
            MaxBytes: maxBytes ?? legacy?.MaxBytes,
            MaxRetainedFiles: ReadInt(bag, "maxRetainedFiles") ?? legacy?.MaxRetainedFiles,
            LogQueueSize: legacy?.LogQueueSize,
            StartMinute: legacy?.StartMinute ?? 0,
            CaptureDurationMinutes: legacy?.CaptureDurationMinutes ?? 60,
            FileNameSuffix: pattern ?? legacy?.FileNameSuffix,
            Locale: legacy?.Locale,
            RetentionDays: retentionDays ?? legacy?.RetentionDays,
            TotalSizeCapBytes: totalCap ?? legacy?.TotalSizeCapBytes);
    }

    // Parse `10MB`, `1GB`, `500KB`, `0`, `""`. Returns null when the
    // input is empty or unparsable so the rolling writer treats it as
    // "no cap" rather than failing the build.
    private static long? ParseHumanByteSize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        long multiplier = 1;
        if (s.EndsWith("GB", StringComparison.OrdinalIgnoreCase)) { multiplier = 1_073_741_824L; s = s[..^2]; }
        else if (s.EndsWith("MB", StringComparison.OrdinalIgnoreCase)) { multiplier = 1_048_576L; s = s[..^2]; }
        else if (s.EndsWith("KB", StringComparison.OrdinalIgnoreCase)) { multiplier = 1_024L; s = s[..^2]; }
        else if (s.EndsWith("B",  StringComparison.OrdinalIgnoreCase)) { multiplier = 1L; s = s[..^1]; }

        if (!double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return null;
        if (n <= 0) return null;
        return (long)Math.Round(n * multiplier);
    }
}
