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

namespace Herald.Sinks.InfluxDB.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="InfluxDBLogSink"/> from
/// a <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// <para>
/// Configuration sources, in priority order:
/// <list type="bullet">
///   <item><b>v2 property bag</b> (<c>configuration-influxdb.mmpform</c> → <see cref="LoggingRuntimeSinkDefinition.Properties"/>):
///     <list type="bullet">
///       <item><c>server_url</c> — InfluxDB v2 base URL (required).</item>
///       <item><c>organization</c> — Influx organization name or ID (required).</item>
///       <item><c>bucket</c> — destination bucket (required).</item>
///       <item><c>token</c> — API token with write permission (required).</item>
///     </list>
///   </item>
///   <item><b>Legacy slots</b> (forward-compatibility — InfluxDB had
///         no legacy JSON-config path before the v2 bag):
///     <list type="bullet">
///       <item><see cref="LoggingRuntimeSinkDefinition.Uri"/> ↔ server_url.</item>
///       <item><see cref="LoggingRuntimeSinkDefinition.Host"/> ↔ organization.</item>
///       <item><see cref="LoggingRuntimeSinkDefinition.Alias"/> ↔ token.</item>
///     </list>
///     <c>bucket</c> has no legacy mapping; older deployments used
///     the code-first ctor.
///   </item>
/// </list>
/// </para>
/// </remarks>
public sealed class InfluxDBLogSinkProvider : BatchingSinkProviderBase
{
    public const string KindKey = "influxdb";
    public override string SinkKind => KindKey;
    public override HeraldEdition MinimumEdition => HeraldEdition.Community;

    public override ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var resolved = InfluxDBSinkRuntimeConfig.From(definition);

        if (string.IsNullOrWhiteSpace(resolved.ServerUrl))
        {
            throw new ArgumentException(
                $"InfluxDB sink '{definition.Name}' is missing the required 'server_url' property " +
                $"(or legacy Uri). Set it in configuration-influxdb.mmpform.",
                nameof(definition));
        }
        if (string.IsNullOrWhiteSpace(resolved.Organization))
        {
            throw new ArgumentException(
                $"InfluxDB sink '{definition.Name}' is missing the required 'organization' property " +
                $"(or legacy Host). Set it in configuration-influxdb.mmpform.",
                nameof(definition));
        }
        if (string.IsNullOrWhiteSpace(resolved.Bucket))
        {
            throw new ArgumentException(
                $"InfluxDB sink '{definition.Name}' is missing the required 'bucket' property. " +
                $"Set it in configuration-influxdb.mmpform.",
                nameof(definition));
        }
        if (string.IsNullOrWhiteSpace(resolved.Token))
        {
            throw new ArgumentException(
                $"InfluxDB sink '{definition.Name}' is missing the required 'token' property " +
                $"(or legacy Alias). Set it in configuration-influxdb.mmpform.",
                nameof(definition));
        }

        var sink = new InfluxDBLogSink(
            serverUrl: resolved.ServerUrl!,
            organization: resolved.Organization!,
            bucket: resolved.Bucket!,
            token: resolved.Token!,
            preserveProperties: resolved.PreserveProperties,
            preserveFieldLimit: resolved.PreserveFieldLimit);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }
}
