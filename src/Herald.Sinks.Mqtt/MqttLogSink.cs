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
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.Mqtt;

/// <summary>
/// Sink that publishes log events as JSON messages to an MQTT broker
/// via MQTTnet. Uses QoS AtMostOnce by default for low-latency
/// delivery; bump QoS via the code-first overload for durable IoT
/// flows.
/// </summary>
public sealed class MqttLogSink : ILogger, IDisposable
{
    private readonly IMqttClient _client;
    private readonly string _topic;
    private readonly MqttQualityOfServiceLevel _qos;
    private readonly bool _ownsClient;

    public MqttLogSink(string brokerHost, int brokerPort = 1883, string topic = "herald/logs")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(brokerHost);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        _client = new MqttFactory().CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(brokerHost, brokerPort)
            .Build();
        _client.ConnectAsync(options).GetAwaiter().GetResult();
        _topic = topic;
        _qos = MqttQualityOfServiceLevel.AtMostOnce;
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

    public void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(_topic)
            .WithPayload(SerializeEvent(logEvent))
            .WithQualityOfServiceLevel(_qos)
            .Build();
        _client.PublishAsync(message).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            try { _client.DisconnectAsync().GetAwaiter().GetResult(); }
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
