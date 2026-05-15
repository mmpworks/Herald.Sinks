// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Events;
using MMP.Herald.Services;
using RabbitMQ.Client;

namespace Herald.Sinks.RabbitMQ;

/// <summary>
/// Sink that publishes log events to a RabbitMQ exchange. Each event
/// becomes a JSON-encoded AMQP message routed by a caller-supplied
/// routing key. Downstream consumers decide what to do — Graylog,
/// Logstash, a custom analytics pipeline, a retention store.
/// </summary>
/// <remarks>
/// <para>
/// <b>Connection reuse.</b> A single <see cref="IConnection"/> and
/// <see cref="IModel"/> (channel) are held for the sink's lifetime.
/// Publishes reuse the channel; the RabbitMQ client is thread-safe
/// for BasicPublish on a single channel as of RabbitMQ.Client 7.x.
/// </para>
/// <para>
/// <b>Durability.</b> By default messages carry <c>persistent=true</c>
/// so a broker restart does not lose pending events. The target
/// exchange must itself be durable for persistence to matter — that
/// is a broker-side declaration owned by the installer, not the
/// sink.
/// </para>
/// <para>
/// <b>Exchange type.</b> The sink assumes the target exchange already
/// exists. Pick <c>topic</c> for pattern-based routing, <c>direct</c>
/// for exact routing-key match, or <c>fanout</c> to broadcast all
/// events to every bound queue.
/// </para>
/// </remarks>
public sealed class RabbitMQLogSink : HeraldSinkBase, IDisposable, INetworkSink
{
    private readonly string _exchange;
    private readonly string _routingKey;
    private readonly bool _persistent;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly object _publishLock = new();
    private int _disposed;

    public RabbitMQLogSink(
        string amqpUri,
        string exchange,
        string routingKey = "",
        bool persistent = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(amqpUri);
        ArgumentNullException.ThrowIfNull(exchange);  // empty exchange = default exchange; allow

        _exchange = exchange;
        _routingKey = routingKey ?? string.Empty;
        _persistent = persistent;

        var factory = new ConnectionFactory
        {
            Uri = new Uri(amqpUri),
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        if (Volatile.Read(ref _disposed) == 1) return;

        var body = BuildBody(logEvent);
        var props = _channel.CreateBasicProperties();
        props.ContentType = "application/json";
        props.DeliveryMode = (byte)(_persistent ? 2 : 1);
        props.Timestamp = new AmqpTimestamp(logEvent.TimeUtc.ToUnixTimeSeconds());

        // BasicPublish is thread-safe at the channel level in
        // RabbitMQ.Client 7.x, but pairing it with CreateBasicProperties
        // under concurrent callers is not — they share the channel's
        // property pool. Cheap lock; the publish itself is small.
        lock (_publishLock)
        {
            _channel.BasicPublish(
                exchange: _exchange,
                routingKey: _routingKey,
                basicProperties: props,
                body: body);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        try { _channel.Close(); } catch { }
        try { _channel.Dispose(); } catch { }
        try { _connection.Close(); } catch { }
        try { _connection.Dispose(); } catch { }
    }

    private static byte[] BuildBody(LogEvent evt)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("time_utc", evt.TimeUtc.UtcDateTime.ToString("O"));
            writer.WriteString("level", evt.Level.Key);
            writer.WriteString("category", evt.Category.Value);
            writer.WriteString("message", evt.Message ?? string.Empty);
            writer.WriteString("template", evt.MessageTemplate ?? string.Empty);

            if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value) && value is Exception ex)
            {
                writer.WriteString("exception", ex.ToString());
            }

            if (evt.Properties is not null && evt.Properties.Count > 0)
            {
                writer.WriteStartObject("properties");
                foreach (var prop in evt.Properties)
                {
                    writer.WriteString(prop.Name, prop.ResolvedValue?.ToString());
                }
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.Flush();
        }
        return stream.ToArray();
    }
}
