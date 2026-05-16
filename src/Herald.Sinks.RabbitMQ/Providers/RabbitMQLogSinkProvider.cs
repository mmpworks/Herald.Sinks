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

namespace Herald.Sinks.RabbitMQ.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="RabbitMQLogSink"/> from a
/// <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// Wire-up:
/// <list type="bullet">
///   <item><c>Uri</c> → AMQP connection URI (required). Example
///   <c>amqp://user:pass@host:5672/vhost</c>.</item>
///   <item><c>Host</c> → <c>exchange/routingKey</c>, default
///   <c>herald.logs/</c> (empty routing key).</item>
///   <item><c>Alias</c> → when set to <c>transient</c>, messages are
///   published non-persistent.</item>
/// </list>
/// </remarks>
public sealed class RabbitMQLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "rabbitmq";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);

        var (exchange, routingKey) = ParseHost(definition.Host);
        var persistent = !string.Equals(definition.Alias, "transient", StringComparison.OrdinalIgnoreCase);

        return new RabbitMQLogSink(
            amqpUri: definition.Uri,
            exchange: exchange,
            routingKey: routingKey,
            persistent: persistent);
    }

    private static (string Exchange, string RoutingKey) ParseHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return ("herald.logs", string.Empty);

        var slash = host.IndexOf('/');
        if (slash < 0) return (host, string.Empty);

        var exchange = host[..slash].Trim();
        var routingKey = host[(slash + 1)..].Trim();
        if (exchange.Length == 0) exchange = "herald.logs";
        return (exchange, routingKey);
    }
}
