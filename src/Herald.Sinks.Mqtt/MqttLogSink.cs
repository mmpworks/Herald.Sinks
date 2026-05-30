// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.IO;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.Mqtt;

/// <summary>
/// Sink that publishes log events as JSON messages to an MQTT broker
/// via MQTTnet. Uses QoS AtMostOnce by default for low-latency
/// delivery; bump QoS via the code-first overload for durable IoT
/// flows.
/// </summary>
public sealed class MqttLogSink : HeraldSinkBase, IDisposable, INetworkSink
{
    private readonly IMqttClient _client;
    private readonly string _topic;
    private readonly MqttQualityOfServiceLevel _qos;
    private readonly bool _ownsClient;

    /// <summary>
    /// Create an MQTT sink that opens its own connection to the broker.
    /// <paramref name="username"/> + <paramref name="password"/> enable
    /// MQTT user-property auth (plaintext on the wire). The
    /// <paramref name="qos"/> argument applies to every published
    /// message; AtMostOnce is the default for low-latency log shipping
    /// (drop-on-disconnect is acceptable). Bump to AtLeastOnce or
    /// ExactlyOnce for IoT flows that need delivery guarantees, at
    /// the cost of a broker round-trip per event.
    /// </summary>
    /// <remarks>
    /// MQTTS (TLS-wrapped MQTT) is intentionally not configurable here —
    /// that combination belongs to the Compliance-edition TLS sub-track
    /// and lands when TLS plumbing arrives uniformly across the
    /// plaintext sinks.
    /// </remarks>
    public MqttLogSink(
        string brokerHost,
        int brokerPort = 1883,
        string topic = "herald/logs",
        string? username = null,
        string? password = null,
        MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(brokerHost);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        _client = new MqttFactory().CreateMqttClient();
        // Union of two reconciled changes: the conformance lineage added MQTT
        // user-property credential auth; the deadlock fix added ConfigureAwait(false)
        // so this blocking connect can't deadlock on a SynchronizationContext-bearing
        // thread. Both intents are kept — dropping either is a regression.
        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(brokerHost, brokerPort);
        if (!string.IsNullOrWhiteSpace(username))
        {
            optionsBuilder = optionsBuilder.WithCredentials(username, password);
        }
        _client.ConnectAsync(optionsBuilder.Build()).ConfigureAwait(false).GetAwaiter().GetResult();
        _topic = topic;
        _qos = qos;
        _ownsClient = true;
    }

    public MqttLogSink(IMqttClient client, string topic = "herald/logs",
        MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        _client = client;
        _topic = topic;
        _qos = qos;
        _ownsClient = false;
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(_topic)
            .WithPayload(SerializeEvent(logEvent))
            .WithQualityOfServiceLevel(_qos)
            .Build();
        _client.PublishAsync(message).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            try { _client.DisconnectAsync().ConfigureAwait(false).GetAwaiter().GetResult(); }
            catch { /* best-effort on shutdown */ }
            _client.Dispose();
        }
    }

    private static string SerializeEvent(LogEvent evt)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("time_utc", evt.TimeUtc);
            writer.WriteString("level", evt.Level.Key);
            writer.WriteString("category", evt.Category.Value);
            writer.WriteString("message", evt.Message ?? string.Empty);
            writer.WriteString("template", evt.MessageTemplate ?? string.Empty);
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
