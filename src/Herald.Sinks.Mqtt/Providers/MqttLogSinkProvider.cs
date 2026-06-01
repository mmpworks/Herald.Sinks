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
///       <item><c>username</c> — optional MQTT username.</item>
///       <item><c>password</c> — optional MQTT password.</item>
///       <item><c>qos</c> — <c>at_most_once</c> / <c>at_least_once</c> /
///             <c>exactly_once</c> (default <c>at_most_once</c>).</item>
///     </list>
///   </item>
///   <item><b>Legacy slots</b> (pre-v2 dashboard JSON):
///     <list type="bullet">
///       <item><see cref="LoggingRuntimeSinkDefinition.Uri"/> ↔ broker.</item>
///       <item><see cref="LoggingRuntimeSinkDefinition.Host"/> ↔ topic.</item>
///     </list>
///     Auth and QoS had no legacy slot mapping — older deployments
///     used the client-injected code-first overload.
///   </item>
/// </list>
/// MQTTS (TLS-wrapped MQTT) belongs to the Compliance-edition TLS
/// sub-track and is intentionally not configured here.
/// </para>
/// </remarks>
public sealed class MqttLogSinkProvider : BatchingSinkProviderBase
{
    public const string KindKey = "mqtt";
    public override string SinkKind => KindKey;
    public override HeraldEdition MinimumEdition => HeraldEdition.Community;

    public override ILogger CreateSink(
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

        var sink = new MqttLogSink(
            brokerHost: resolved.BrokerHost!,
            brokerPort: resolved.BrokerPort,
            topic: resolved.Topic,
            username: resolved.Username,
            password: resolved.Password,
            qos: resolved.Qos);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }
}
