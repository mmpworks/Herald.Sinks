// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using MMP.Herald.Configuration.Runtime;

namespace Herald.Sinks.InfluxDB.Providers;

/// <summary>
/// Maps a <see cref="LoggingRuntimeSinkDefinition"/> into the values
/// <see cref="InfluxDBLogSink"/>'s constructor needs.
///
/// <para>
/// The InfluxDB mmpform exposes four required fields:
/// <list type="bullet">
///   <item><c>server_url</c> — InfluxDB v2 base URL.</item>
///   <item><c>organization</c> — Influx organization name or ID.</item>
///   <item><c>bucket</c> — destination bucket.</item>
///   <item><c>token</c> — API token with write permission.</item>
/// </list>
/// The previous provider threw <see cref="NotSupportedException"/>
/// because the four-field surface did not fit the three legacy slots.
/// The v2 bag carries it cleanly now.
/// </para>
/// </summary>
internal static class InfluxDBSinkRuntimeConfig
{
    private const string KeyServerUrl    = "server_url";
    private const string KeyOrganization = "organization";
    private const string KeyBucket       = "bucket";
    private const string KeyToken        = "token";

    /// <summary>
    /// Resolved InfluxDB sink config. Required fields stay nullable
    /// so the provider can fail with a single field-named
    /// ArgumentException.
    /// </summary>
    public readonly record struct Resolved(
        string? ServerUrl,
        string? Organization,
        string? Bucket,
        string? Token);

    public static Resolved From(LoggingRuntimeSinkDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var bag = definition.Properties;
        return new Resolved(
            ServerUrl:    ReadString(bag, KeyServerUrl)    ?? Nullify(definition.Uri),
            Organization: ReadString(bag, KeyOrganization) ?? Nullify(definition.Host),
            Bucket:       ReadString(bag, KeyBucket),
            Token:        ReadString(bag, KeyToken)        ?? Nullify(definition.Alias));
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
