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

namespace Herald.Sinks.Mqtt.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="MqttLogSink"/> from a
/// <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// <para>
/// Configuration sources, in priority order:
/// <list type="bullet">
///   <item><b>v2 property bag</b> (<c>configuration-mqtt.mmpform</c> → <see cref="LoggingRuntimeSinkDefinition.Properties"/>):
///     <list type="bullet">
///       <item><c>broker</c> — host[:port] of the MQTT broker (required).
///             Port defaults to 1883 when absent.</item>
///       <item><c>topic</c> — MQTT topic (default <c>herald/logs</c>).
///             Hierarchy supported (e.g. <c>herald/logs/error</c>).</item>
///     </list>
///   </item>
///   <item><b>Legacy slots</b> (pre-v2 dashboard JSON):
///     <list type="bullet">
///       <item><see cref="LoggingRuntimeSinkDefinition.Uri"/> ↔ broker.</item>
///       <item><see cref="LoggingRuntimeSinkDefinition.Host"/> ↔ topic.</item>
///     </list>
///     Read only when the bag does not supply the value, so deployments
///     authored before the v2 form rolled out keep working without
///     form re-entry.
///   </item>
/// </list>
/// Auth, MQTTS/TLS, and QoS — available on the sink's code-first
/// overload — are not surfaced through this provider yet. They
/// belong to the BLOCKER expansion of the Mqtt mmpform tracked in
/// the sinks-config audit (Pass-2 Step 4).
/// </para>
/// </remarks>
public sealed class MqttLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "mqtt";
    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var resolved = MqttSinkRuntimeConfig.From(definition);

        if (string.IsNullOrWhiteSpace(resolved.BrokerHost))
        {
            throw new ArgumentException(
                $"Mqtt sink '{definition.Name}' is missing the required 'broker' property " +
                $"(or legacy Uri). Set it in configuration-mqtt.mmpform.",
                nameof(definition));
        }

        return new MqttLogSink(
            brokerHost: resolved.BrokerHost!,
            brokerPort: resolved.BrokerPort,
            topic: resolved.Topic);
    }
}
