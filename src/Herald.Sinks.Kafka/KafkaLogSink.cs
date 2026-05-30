// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Confluent.Kafka;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.Kafka;

/// <summary>
/// Sink that produces log events as JSON messages to a Kafka topic
/// via the Confluent.Kafka client. Drop-in equivalent for
/// Serilog.Sinks.Kafka.
/// </summary>
/// <remarks>
/// <para>
/// <b>Producer ownership.</b> The connection-string ctor builds and
/// owns an <see cref="IProducer{TKey, TValue}"/> with sensible
/// defaults (acks=all, linger.ms=5, compression=snappy). Apps that
/// already share a producer with their domain code use the code-first
/// overload that takes an <see cref="IProducer{TKey, TValue}"/>
/// directly — the sink does not dispose it.
/// </para>
/// <para>
/// <b>Partition key.</b> By default messages are produced without a
/// key (round-robin partitioning). Pass a non-null
/// <see cref="KeyAccessor"/> to derive a key from each event — the
/// usual choice is event category or a tenant id so related events
/// stick to a partition for ordered consumption.
/// </para>
/// <para>
/// <b>Backpressure.</b> The Confluent producer buffers internally
/// (queue.buffering.max.messages); a full queue throws when Produce
/// is called. Pair with Herald's WithAsyncLogging for safety under
/// load.
/// </para>
/// <para>
/// <b>Thread safety.</b> Confluent.Kafka producers are thread-safe
/// per the SDK contract; concurrent Log calls share the producer's
/// internal connection pool.
/// </para>
/// </remarks>
public sealed class KafkaLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable, INetworkSink
{
    private readonly IProducer<string?, string> _producer;
    private readonly string _topic;
    private readonly Func<LogEvent, string?>? _keyAccessor;
    private readonly bool _ownsProducer;

    /// <summary>
    /// Create a Kafka sink. Pass <paramref name="saslMechanism"/>
    /// (one of <c>PLAIN</c>, <c>SCRAM-SHA-256</c>, <c>SCRAM-SHA-512</c>)
    /// plus <paramref name="saslUsername"/> and
    /// <paramref name="saslPassword"/> to enable SASL_PLAINTEXT auth.
    /// Leaving the mechanism null produces an unauthenticated client,
    /// which matches the prior behaviour.
    /// </summary>
    /// <remarks>
    /// SASL_SSL (TLS-wrapped SASL) is intentionally not configurable
    /// here — that combination belongs to the Compliance-edition TLS
    /// sub-track and lands when TLS plumbing arrives uniformly across
    /// the plaintext sinks.
    /// </remarks>
    public KafkaLogSink(
        string bootstrapServers,
        string topic,
        Func<LogEvent, string?>? keyAccessor = null,
        string? saslMechanism = null,
        string? saslUsername = null,
        string? saslPassword = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bootstrapServers);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            LingerMs = 5,
            CompressionType = CompressionType.Snappy,
        };

        // SASL wiring: only enable when the operator named a mechanism.
        // Pre-validating the mechanism here gives a clearer message
        // than rdkafka's connect-time error. SASL_SSL is intentionally
        // out of scope (see remarks).
        if (!string.IsNullOrWhiteSpace(saslMechanism))
        {
            config.SecurityProtocol = SecurityProtocol.SaslPlaintext;
            config.SaslMechanism = ParseSaslMechanism(saslMechanism);
            config.SaslUsername = saslUsername;
            config.SaslPassword = saslPassword;
        }

        _producer = new ProducerBuilder<string?, string>(config).Build();
        _topic = topic;
        _keyAccessor = keyAccessor;
        _ownsProducer = true;
    }

    // Mechanism strings come from the mmpform as operator-typed values.
    // Anything we don't recognise becomes ArgumentException — better
    // than letting rdkafka surface a less obvious error at first send.
    private static SaslMechanism ParseSaslMechanism(string raw) =>
        raw.Trim().ToUpperInvariant() switch
        {
            "PLAIN"         => SaslMechanism.Plain,
            "SCRAM-SHA-256" => SaslMechanism.ScramSha256,
            "SCRAM-SHA-512" => SaslMechanism.ScramSha512,
            _ => throw new ArgumentException(
                $"Unknown SASL mechanism '{raw}'. Supported: PLAIN, SCRAM-SHA-256, SCRAM-SHA-512.",
                nameof(raw)),
        };

    /// <summary>
    /// Code-first overload accepts a pre-built
    /// <see cref="IProducer{TKey, TValue}"/>. The sink does not
    /// dispose the producer because it does not own it.
    /// </summary>
    public KafkaLogSink(IProducer<string?, string> producer, string topic, Func<LogEvent, string?>? keyAccessor = null)
    {
        ArgumentNullException.ThrowIfNull(producer);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        _producer = producer;
        _topic = topic;
        _keyAccessor = keyAccessor;
        _ownsProducer = false;
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        _producer.Produce(_topic, BuildMessage(logEvent));
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        // The Confluent producer batches internally; per-event Produce
        // calls accumulate into rdkafka's send queue and the linger.ms
        // setting flushes them as one network request. A Flush() at the
        // end pushes anything still in the queue so the call doesn't
        // return before the batch is on the wire.
        foreach (var evt in events)
        {
            _producer.Produce(_topic, BuildMessage(evt));
        }
        _producer.Flush(TimeSpan.FromSeconds(5));
    }

    public void Dispose()
    {
        if (_ownsProducer)
        {
            // Flush before dispose so in-flight messages aren't lost.
            try { _producer.Flush(TimeSpan.FromSeconds(5)); }
            catch (KafkaException) { /* best-effort on shutdown */ }
            _producer.Dispose();
        }
    }

    private Message<string?, string> BuildMessage(LogEvent evt)
    {
        var key = _keyAccessor?.Invoke(evt);
        return new Message<string?, string>
        {
            Key = key,
            Value = SerializeEvent(evt),
        };
    }

    private static string SerializeEvent(LogEvent evt)
    {
        // Utf8JsonWriter keeps the path AOT-clean.
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
