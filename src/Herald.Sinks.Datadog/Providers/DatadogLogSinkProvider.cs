// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using MMP.Herald;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Levels;
using MMP.Herald.Output.Rendering;
using MMP.Herald.Pipeline;
using MMP.Herald.Routing;
using MMP.Herald.Sinks.Batching;

namespace Herald.Sinks.Datadog.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="DatadogLogSink"/> from
/// a <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// <para>
/// Configuration sources, in priority order:
/// <list type="bullet">
///   <item><b>v2 property bag</b> (<c>configuration-datadog.mmpform</c> → <see cref="LoggingRuntimeSinkDefinition.Properties"/>):
///     <list type="bullet">
///       <item><c>endpoint</c> — Datadog HTTP log intake URL or local Agent address.</item>
///       <item><c>api_key</c> — Datadog DD-API-KEY (required).</item>
///       <item><c>service</c> — Datadog service tag attached to every event (required).</item>
///     </list>
///   </item>
///   <item><b>Legacy slots</b> (pre-v2 dashboard JSON):
///     <list type="bullet">
///       <item><see cref="LoggingRuntimeSinkDefinition.Uri"/> ↔ endpoint.</item>
///       <item><see cref="LoggingRuntimeSinkDefinition.Alias"/> ↔ api_key.</item>
///       <item><see cref="LoggingRuntimeSinkDefinition.Host"/> ↔ service.</item>
///     </list>
///     Read only when the bag does not supply the value, so deployments
///     authored before the v2 form rolled out keep working without
///     form re-entry.
///   </item>
/// </list>
/// </para>
/// </remarks>
public sealed class DatadogLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "datadog";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var resolved = DatadogSinkRuntimeConfig.From(definition);

        // api_key + service are required by both the mmpform and the
        // sink constructor. Validating here gives the operator a clear
        // ArgumentException naming the missing field; the constructor's
        // own guards still run as defence-in-depth.
        if (string.IsNullOrWhiteSpace(resolved.ApiKey))
        {
            throw new ArgumentException(
                $"Datadog sink '{definition.Name}' is missing the required 'api_key' property " +
                $"(or legacy Alias). Set it in configuration-datadog.mmpform.",
                nameof(definition));
        }
        if (string.IsNullOrWhiteSpace(resolved.Service))
        {
            throw new ArgumentException(
                $"Datadog sink '{definition.Name}' is missing the required 'service' property " +
                $"(or legacy Host). Set it in configuration-datadog.mmpform.",
                nameof(definition));
        }

        var sink = new DatadogLogSink(
            apiKey: resolved.ApiKey!,
            service: resolved.Service!,
            endpoint: resolved.Endpoint);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }
}
