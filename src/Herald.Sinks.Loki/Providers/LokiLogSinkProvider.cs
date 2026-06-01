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

namespace Herald.Sinks.Loki.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="LokiLogSink"/> from a
/// <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// <para>
/// Configuration sources, in priority order:
/// <list type="bullet">
///   <item><b>v2 property bag</b> (<c>configuration-loki.mmpform</c> → <see cref="LoggingRuntimeSinkDefinition.Properties"/>):
///     <list type="bullet">
///       <item><c>endpoint</c> — Loki push URL (required).</item>
///       <item><c>bearer_token</c> — optional Grafana Cloud bearer token.</item>
///     </list>
///   </item>
///   <item><b>Legacy slots</b> (pre-v2 dashboard JSON):
///     <list type="bullet">
///       <item><see cref="LoggingRuntimeSinkDefinition.Uri"/> ↔ endpoint.</item>
///       <item><see cref="LoggingRuntimeSinkDefinition.Alias"/> ↔ bearer_token.</item>
///     </list>
///     Read only when the bag does not supply the value, so deployments
///     authored before the v2 form rolled out keep working without
///     form re-entry.
///   </item>
/// </list>
/// Static labels and Basic Auth — both available on the sink
/// constructor — are not surfaced through this provider yet. They
/// belong to the BLOCKER expansion of the Loki mmpform tracked in
/// the sinks-config audit (Pass-2 Step 4).
/// </para>
/// </remarks>
public sealed class LokiLogSinkProvider : BatchingSinkProviderBase
{
    public const string KindKey = "loki";

    public override string SinkKind => KindKey;
    public override HeraldEdition MinimumEdition => HeraldEdition.Community;

    public override ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var resolved = LokiSinkRuntimeConfig.From(definition);

        if (string.IsNullOrWhiteSpace(resolved.Endpoint))
        {
            throw new ArgumentException(
                $"Loki sink '{definition.Name}' is missing the required 'endpoint' property " +
                $"(or legacy Uri). Set it in configuration-loki.mmpform.",
                nameof(definition));
        }

        var sink = new LokiLogSink(
            endpoint: resolved.Endpoint!,
            bearerToken: resolved.BearerToken);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }
}
