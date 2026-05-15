// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using StackExchange.Redis;

namespace Herald.Sinks.Redis;

/// <summary>
/// Sink that PUBLISHes each log event as a JSON message to a Redis
/// channel. Pairs with subscribers using <c>SUBSCRIBE</c> / <c>PSUBSCRIBE</c>
/// for fan-out delivery to dashboards, alert routers, or downstream
/// log shippers. Drop-in equivalent for Serilog.Sinks.Redis (PubSub).
/// </summary>
/// <remarks>
/// <para>
/// <b>Delivery semantics.</b> Redis PubSub is fire-and-forget; messages
/// published while no subscriber is connected are lost. For durable
/// delivery use <c>Herald.Sinks.RedisList</c> (RPUSH into a List that
/// downstream workers BRPOPLPUSH out of).
/// </para>
/// <para>
/// <b>Connection sharing.</b> The sink owns the <see cref="ConnectionMultiplexer"/>
/// it builds from a connection string. Apps that already share a multiplexer
/// across the process should use the code-first overload that takes an
/// <see cref="ISubscriber"/> directly.
/// </para>
/// <para>
/// <b>Thread safety.</b> StackExchange.Redis multiplexers and subscribers
/// are thread-safe per the driver contract; concurrent <c>Log</c> calls
/// share the multiplexer's connection pool.
/// </para>
/// </remarks>
public sealed class RedisLogSink : HeraldSinkBase, IDisposable, INetworkSink
{
    private readonly ISubscriber _subscriber;
    private readonly RedisChannel _channel;
    private readonly ConnectionMultiplexer? _ownedMultiplexer;

    public RedisLogSink(string connectionString, string channelName = "herald-logs")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);

        _ownedMultiplexer = ConnectionMultiplexer.Connect(connectionString);
        _subscriber = _ownedMultiplexer.GetSubscriber();
        _channel = RedisChannel.Literal(channelName);
    }

    /// <summary>
    /// Code-first overload for callers that already own a connected
    /// <see cref="ISubscriber"/>. The sink does not dispose the
    /// subscriber on dispose because it does not own it.
    /// </summary>
    public RedisLogSink(ISubscriber subscriber, string channelName = "herald-logs")
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);

        _subscriber = subscriber;
        _channel = RedisChannel.Literal(channelName);
        _ownedMultiplexer = null;
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        var payload = SerializeEvent(logEvent);
        // Publish is fire-and-forget by design; we ignore the
        // returned subscriber count because an event published with no
        // subscribers is still a successful publish per the Redis contract.
        _subscriber.Publish(_channel, payload, CommandFlags.FireAndForget);
    }

    public void Dispose()
    {
        _ownedMultiplexer?.Dispose();
    }

    private static string SerializeEvent(LogEvent evt)
    {
        // Utf8JsonWriter keeps the path AOT-clean (no JsonSerializer.Serialize<T>
        // reflection trim warnings) and avoids per-event allocation of a
        // JsonSerializerOptions instance.
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("time_utc", evt.TimeUtc);
            writer.WriteString("level", evt.Level.Key);
            writer.WriteString("category", evt.Category.Value);
            writer.WriteString("message", evt.Message ?? string.Empty);
            writer.WriteString("template", evt.MessageTemplate ?? string.Empty);

            if (evt.Properties is not null && evt.Properties.Count > 0)
            {
                writer.WriteStartObject("properties");
                foreach (var prop in evt.Properties)
                {
                    WriteJsonValue(writer, prop.Name, prop.ResolvedValue);
                }
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteJsonValue(Utf8JsonWriter writer, string name, object? value)
    {
        // Map common BCL primitives so property values round-trip usefully.
        // Anything outside this set falls back to ToString() — better than
        // throwing on a custom type.
        switch (value)
        {
            case null: writer.WriteNull(name); break;
            case string s: writer.WriteString(name, s); break;
            case bool b: writer.WriteBoolean(name, b); break;
            case int i: writer.WriteNumber(name, i); break;
            case long l: writer.WriteNumber(name, l); break;
            case double d: writer.WriteNumber(name, d); break;
            case float f: writer.WriteNumber(name, f); break;
            case decimal m: writer.WriteNumber(name, m); break;
            case DateTime dt: writer.WriteString(name, dt.ToUniversalTime()); break;
            case DateTimeOffset dto: writer.WriteString(name, dto); break;
            case Guid g: writer.WriteString(name, g); break;
            default: writer.WriteString(name, value.ToString() ?? string.Empty); break;
        }
    }
}
