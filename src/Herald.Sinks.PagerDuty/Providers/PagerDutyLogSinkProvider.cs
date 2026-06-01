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

namespace Herald.Sinks.PagerDuty.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="PagerDutyLogSink"/> from
/// a <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// <para>
/// Configuration sources, in priority order:
/// <list type="bullet">
///   <item><b>v2 property bag</b> (<c>configuration-pagerduty.mmpform</c> → <see cref="LoggingRuntimeSinkDefinition.Properties"/>):
///     <list type="bullet">
///       <item><c>routing_key</c> — PagerDuty integration key (required).</item>
///       <item><c>source</c> — Events API source field
///             (default: machine name).</item>
///       <item><c>component</c> / <c>group</c> — categorisation tags.</item>
///       <item><c>endpoint</c> — enqueue URL.</item>
///       <item><c>dedup_strategy</c> — picks dedup_key derivation
///             (<c>auto</c>, <c>event_id</c>, <c>template</c>,
///             <c>category</c>, <c>message</c>).</item>
///       <item><c>custom_details_template</c> — static fields merged
///             into <c>payload.custom_details</c>.</item>
///     </list>
///   </item>
///   <item><b>Legacy slots</b>:
///     <list type="bullet">
///       <item><see cref="LoggingRuntimeSinkDefinition.Alias"/> ↔ routing_key.</item>
///       <item><see cref="LoggingRuntimeSinkDefinition.Host"/> ↔ source.</item>
///       <item><see cref="LoggingRuntimeSinkDefinition.Uri"/> ↔ endpoint.</item>
///     </list>
///     <c>dedup_strategy</c> and <c>custom_details_template</c> had no
///     legacy slot — older deployments either used the code-first ctor
///     or ran on the default auto-derived dedup chain.
///   </item>
/// </list>
/// </para>
/// </remarks>
public sealed class PagerDutyLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "pagerduty";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var resolved = PagerDutySinkRuntimeConfig.From(definition);

        if (string.IsNullOrWhiteSpace(resolved.RoutingKey))
        {
            throw new ArgumentException(
                $"PagerDuty sink '{definition.Name}' is missing the required 'routing_key' property " +
                $"(or legacy Alias). Set it in configuration-pagerduty.mmpform.",
                nameof(definition));
        }

        var sink = new PagerDutyLogSink(
            routingKey: resolved.RoutingKey!,
            source: resolved.Source,
            component: resolved.Component,
            group: resolved.Group,
            endpoint: resolved.Endpoint,
            dedupStrategy: resolved.DedupStrategy,
            customDetailsTemplate: resolved.CustomDetailsTemplate);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }
}
