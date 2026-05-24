// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using MMP.Herald.Configuration.Runtime;

namespace Herald.Sinks.Bugsnag.Providers;

/// <summary>
/// Maps a <see cref="LoggingRuntimeSinkDefinition"/> into the values
/// <see cref="BugsnagLogSink"/>'s constructor needs.
///
/// <para>
/// The Bugsnag mmpform exposes three fields:
/// <list type="bullet">
///   <item><c>api_key</c> — Bugsnag project API key (required).</item>
///   <item><c>endpoint</c> — notify endpoint (default
///         <c>https://notify.bugsnag.com/</c>).</item>
///   <item><c>release_stage</c> — release stage tag (default
///         <c>production</c>). Drives Bugsnag's dashboard filtering —
///         flagged in Richard's audit (BLOCKER) because operators
///         shipping any environment other than production silently
///         pile events into the wrong bucket without it.</item>
/// </list>
/// Older deployments authored before the v2 bag existed put the API
/// key in the <c>Uri</c> slot — the legacy fallback keeps that
/// behaviour. <c>release_stage</c> has no legacy slot; older
/// deployments could only opt in via the code-first ctor.
/// </para>
/// </summary>
internal static class BugsnagSinkRuntimeConfig
{
    private const string KeyApiKey       = "api_key";
    private const string KeyEndpoint     = "endpoint";
    private const string KeyReleaseStage = "release_stage";

    /// <summary>
    /// Resolved Bugsnag sink config. <see cref="ApiKey"/> stays
    /// nullable so the provider can fail with a named
    /// ArgumentException; the other fields carry their defaults.
    /// </summary>
    public readonly record struct Resolved(
        string? ApiKey,
        string? Endpoint,
        string? ReleaseStage);

    public static Resolved From(LoggingRuntimeSinkDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var bag = definition.Properties;
        return new Resolved(
            // Legacy: api key rode in definition.Uri historically — the
            // sink ctor never accepted endpoint via the slot, only via
            // the keyword arg. So Uri stays the legacy api_key slot.
            ApiKey:       ReadString(bag, KeyApiKey)       ?? Nullify(definition.Uri),
            Endpoint:     ReadString(bag, KeyEndpoint),
            ReleaseStage: ReadString(bag, KeyReleaseStage));
    }

    private static string? ReadString(
        IReadOnlyDictionary<string, object?>? bag, string key)
    {
        if (bag is null) return null;
        if (!bag.TryGetValue(key, out var raw) || raw is null) return null;
        var text = raw.ToString();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static string? Nullify(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
