// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Configuration.Sinks;

namespace Herald.Sinks.Loki.Providers;

/// <summary>
/// Maps a <see cref="LoggingRuntimeSinkDefinition"/> into the values
/// <see cref="LokiLogSink"/>'s constructor needs.
///
/// <para>
/// The Loki mmpform exposes two operator-facing fields today:
/// <list type="bullet">
///   <item><c>endpoint</c> — Loki push URL (required).</item>
///   <item><c>bearer_token</c> — optional Grafana Cloud bearer token.</item>
/// </list>
/// Older deployments authored before the v2 bag existed populated
/// the same values through the definition's <c>Uri</c> and
/// <c>Alias</c> slots; this mapper accepts either source.
/// </para>
///
/// <para>
/// Static labels and Basic Auth — both exposed by the sink constructor
/// — are intentionally not surfaced here. They live behind the
/// mmpform's future expansion path (Richard's audit BLOCKER row for
/// Loki); reading them today would let a hand-crafted bag bypass the
/// form, which is exactly the drift the migration is trying to close.
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
internal static class LokiSinkRuntimeConfig
{
    private const string KeyEndpoint    = "endpoint";
    private const string KeyBearerToken = "bearer_token";

    /// <summary>
    /// Resolved Loki sink config: every field carries either the bag
    /// value or the matching legacy slot, with null when neither is
    /// set. The provider validates required values before handing the
    /// record off to the sink constructor.
    /// </summary>
    public readonly record struct Resolved(
        string? Endpoint,
        string? BearerToken);

    public static Resolved From(LoggingRuntimeSinkDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var bag = definition.Properties;
        return new Resolved(
            Endpoint:    SinkPropertyBag.ReadString(bag, KeyEndpoint)    ?? SinkPropertyBag.Nullify(definition.Uri),
            BearerToken: SinkPropertyBag.ReadString(bag, KeyBearerToken) ?? SinkPropertyBag.Nullify(definition.Alias));
    }
}
