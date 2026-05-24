// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Configuration.Sinks;

namespace Herald.Sinks.Datadog.Providers;

/// <summary>
/// Maps a <see cref="LoggingRuntimeSinkDefinition"/> into the values
/// <see cref="DatadogLogSink"/>'s constructor needs.
///
/// <para>
/// The Datadog sink takes three operator-facing fields:
/// <list type="bullet">
///   <item><c>endpoint</c> — Datadog HTTP log intake URL or local Agent address.</item>
///   <item><c>api_key</c> — Datadog DD-API-KEY (required).</item>
///   <item><c>service</c> — service tag attached to every event.</item>
/// </list>
/// <c>configuration-datadog.mmpform</c> already declares all three;
/// this mapper finally wires them through. Older deployments authored
/// before the v2 bag existed populated the same values through the
/// definition's legacy slots, so each field accepts either source.
/// </para>
///
/// <para>
/// Reading order is bag-first: any populated
/// <see cref="LoggingRuntimeSinkDefinition.Properties"/> wins over
/// the legacy slot. That lets a freshly-written mmpform override an
/// older dashboard JSON without breaking deployments still carrying
/// values in the typed slots.
/// </para>
/// </summary>
internal static class DatadogSinkRuntimeConfig
{
    private const string KeyEndpoint = "endpoint";
    private const string KeyApiKey   = "api_key";
    private const string KeyService  = "service";

    /// <summary>
    /// Resolved Datadog sink config: every field carries either the
    /// bag value or the matching legacy slot, with null when neither
    /// is set. The provider throws on missing required values when it
    /// hands the record off to the sink constructor.
    /// </summary>
    public readonly record struct Resolved(
        string? Endpoint,
        string? ApiKey,
        string? Service);

    public static Resolved From(LoggingRuntimeSinkDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var bag = definition.Properties;
        return new Resolved(
            Endpoint: SinkPropertyBag.ReadString(bag, KeyEndpoint) ?? SinkPropertyBag.Nullify(definition.Uri),
            ApiKey:   SinkPropertyBag.ReadString(bag, KeyApiKey)   ?? SinkPropertyBag.Nullify(definition.Alias),
            Service:  SinkPropertyBag.ReadString(bag, KeyService)  ?? SinkPropertyBag.Nullify(definition.Host));
    }
}
