// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using MMP.Herald.Configuration.Runtime;

namespace Herald.Sinks.Coralogix.Providers;

/// <summary>
/// Maps a <see cref="LoggingRuntimeSinkDefinition"/> into the values
/// <see cref="CoralogixLogSink"/>'s constructor needs.
///
/// <para>
/// The Coralogix mmpform exposes four operator-facing fields:
/// <list type="bullet">
///   <item><c>endpoint</c> — bulk-logs intake URL (default
///         <c>https://ingress.coralogix.com/api/v1/logs</c>).</item>
///   <item><c>private_key</c> — Coralogix private key (required).</item>
///   <item><c>application_name</c> — logical application name (required).</item>
///   <item><c>subsystem_name</c> — component within the application (required).</item>
/// </list>
/// Older deployments authored before the v2 bag existed (and before
/// the Coralogix provider gained a JSON-config path at all) had no
/// legacy slot mapping — the provider threw <see cref="NotSupportedException"/>.
/// That throw is being replaced by this bag-driven path. For
/// continuity with the rest of the Pass-1 / Pass-2 sinks, the mapper
/// still accepts the same Uri/Host/Alias fallback so a future legacy
/// dashboard entry could populate <c>endpoint</c> from <c>Uri</c>.
/// </para>
/// </summary>
internal static class CoralogixSinkRuntimeConfig
{
    private const string KeyEndpoint        = "endpoint";
    private const string KeyPrivateKey      = "private_key";
    private const string KeyApplicationName = "application_name";
    private const string KeySubsystemName   = "subsystem_name";

    /// <summary>
    /// Resolved Coralogix sink config. Required fields are nullable
    /// here so the provider can produce a single field-named
    /// ArgumentException on missing input; the sink constructor's
    /// own guards run as defence-in-depth.
    /// </summary>
    public readonly record struct Resolved(
        string? Endpoint,
        string? PrivateKey,
        string? ApplicationName,
        string? SubsystemName);

    public static Resolved From(LoggingRuntimeSinkDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var bag = definition.Properties;
        return new Resolved(
            Endpoint:        ReadString(bag, KeyEndpoint)        ?? Nullify(definition.Uri),
            PrivateKey:      ReadString(bag, KeyPrivateKey)      ?? Nullify(definition.Alias),
            ApplicationName: ReadString(bag, KeyApplicationName) ?? Nullify(definition.Host),
            SubsystemName:   ReadString(bag, KeySubsystemName));
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
