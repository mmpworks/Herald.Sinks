// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Configuration.Sinks;

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
    private const string KeyServerUrl          = "server_url";
    private const string KeyOrganization       = "organization";
    private const string KeyBucket             = "bucket";
    private const string KeyToken              = "token";
    private const string KeyPreserveProperties = "preserve_properties";
    private const string KeyPreserveFieldLimit = "preserve_field_limit";

    /// <summary>
    /// Resolved InfluxDB sink config. Required fields stay nullable
    /// so the provider can fail with a single field-named
    /// ArgumentException. <see cref="PreserveProperties"/> defaults
    /// false (today's lossy-but-cardinality-safe behaviour);
    /// <see cref="PreserveFieldLimit"/> is the per-event soft cap.
    /// </summary>
    public readonly record struct Resolved(
        string? ServerUrl,
        string? Organization,
        string? Bucket,
        string? Token,
        bool PreserveProperties,
        int PreserveFieldLimit);

    public static Resolved From(LoggingRuntimeSinkDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var bag = definition.Properties;
        return new Resolved(
            ServerUrl:          SinkPropertyBag.ReadString(bag, KeyServerUrl)    ?? SinkPropertyBag.Nullify(definition.Uri),
            Organization:       SinkPropertyBag.ReadString(bag, KeyOrganization) ?? SinkPropertyBag.Nullify(definition.Host),
            Bucket:             SinkPropertyBag.ReadString(bag, KeyBucket),
            Token:              SinkPropertyBag.ReadString(bag, KeyToken)        ?? SinkPropertyBag.Nullify(definition.Alias),
            PreserveProperties: SinkPropertyBag.ReadBool(bag, KeyPreserveProperties) ?? false,
            PreserveFieldLimit: SinkPropertyBag.ReadInt(bag, KeyPreserveFieldLimit) ?? InfluxDBLogSink.DefaultPreserveFieldLimit);
    }
}
