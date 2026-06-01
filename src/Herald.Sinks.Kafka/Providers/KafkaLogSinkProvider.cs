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

namespace Herald.Sinks.Kafka.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="KafkaLogSink"/> from a
/// <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// <para>
/// Configuration sources, in priority order:
/// <list type="bullet">
///   <item><b>v2 property bag</b> (<c>configuration-kafka.mmpform</c> → <see cref="LoggingRuntimeSinkDefinition.Properties"/>):
///     <list type="bullet">
///       <item><c>bootstrap_servers</c> — comma-separated brokers (required).</item>
///       <item><c>topic</c> — destination topic (required).</item>
///       <item><c>sasl_mechanism</c> — <c>PLAIN</c> / <c>SCRAM-SHA-256</c> / <c>SCRAM-SHA-512</c>; empty disables SASL.</item>
///       <item><c>sasl_username</c> — required when <c>sasl_mechanism</c> is set.</item>
///       <item><c>sasl_password</c> — required when <c>sasl_mechanism</c> is set.</item>
///     </list>
///   </item>
///   <item><b>Legacy slots</b>:
///     <list type="bullet">
///       <item><see cref="LoggingRuntimeSinkDefinition.Uri"/> ↔ bootstrap_servers.</item>
///       <item><see cref="LoggingRuntimeSinkDefinition.Host"/> ↔ topic.</item>
///     </list>
///     SASL fields had no legacy slot; older deployments either used
///     the code-first ctor or accepted unauthenticated brokers.
///   </item>
/// </list>
/// SASL_SSL (TLS-wrapped SASL) belongs to the Compliance-edition TLS
/// sub-track and is intentionally not configured here.
/// </para>
/// </remarks>
public sealed class KafkaLogSinkProvider : BatchingSinkProviderBase
{
    public const string KindKey = "kafka";

    public override string SinkKind => KindKey;
    public override HeraldEdition MinimumEdition => HeraldEdition.Community;

    public override ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var resolved = KafkaSinkRuntimeConfig.From(definition);

        if (string.IsNullOrWhiteSpace(resolved.BootstrapServers))
        {
            throw new ArgumentException(
                $"Kafka sink '{definition.Name}' is missing the required 'bootstrap_servers' property " +
                $"(or legacy Uri). Set it in configuration-kafka.mmpform.",
                nameof(definition));
        }
        if (string.IsNullOrWhiteSpace(resolved.Topic))
        {
            throw new ArgumentException(
                $"Kafka sink '{definition.Name}' is missing the required 'topic' property " +
                $"(or legacy Host). Set it in configuration-kafka.mmpform.",
                nameof(definition));
        }

        // Partial SASL config is rejected up front so the operator sees
        // a clear message instead of an rdkafka connect-time error
        // ("authentication failed: missing credentials").
        var hasMech = !string.IsNullOrWhiteSpace(resolved.SaslMechanism);
        var hasUser = !string.IsNullOrWhiteSpace(resolved.SaslUsername);
        var hasPass = !string.IsNullOrWhiteSpace(resolved.SaslPassword);
        if (hasMech && (!hasUser || !hasPass))
        {
            throw new ArgumentException(
                $"Kafka sink '{definition.Name}' set 'sasl_mechanism' but is missing " +
                $"'sasl_username' or 'sasl_password'. Set both in configuration-kafka.mmpform " +
                $"or clear the mechanism to disable SASL.",
                nameof(definition));
        }

        var sink = new KafkaLogSink(
            bootstrapServers: resolved.BootstrapServers!,
            topic: resolved.Topic!,
            keyAccessor: null,
            saslMechanism: resolved.SaslMechanism,
            saslUsername: resolved.SaslUsername,
            saslPassword: resolved.SaslPassword);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }
}
