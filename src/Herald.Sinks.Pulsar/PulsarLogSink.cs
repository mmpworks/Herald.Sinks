// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DotPulsar;
using DotPulsar.Abstractions;
using DotPulsar.Extensions;
using MMP.Herald;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.Pulsar;

/// <summary>
/// Sink that produces log events as messages to an Apache Pulsar
/// topic via DotPulsar. Supports persistent and non-persistent topics
/// with the same code path; the topic URL determines durability.
/// </summary>
public sealed class PulsarLogSink : ILogger, IAsyncDisposable
{
    private readonly IPulsarClient _client;
    private readonly IProducer<byte[]> _producer;
    private readonly bool _ownsClient;

    public PulsarLogSink(string serviceUrl, string topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        _client = PulsarClient.Builder().ServiceUrl(new Uri(serviceUrl)).Build();
        _producer = _client.NewProducer(Schema.ByteArray).Topic(topic).Create();
        _ownsClient = true;
    }

    public PulsarLogSink(IProducer<byte[]> producer)
    {
        ArgumentNullException.ThrowIfNull(producer);
        _producer = producer;
        _client = null!;
        _ownsClient = false;
    }

    public void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        var payload = Encoding.UTF8.GetBytes(SerializeEvent(logEvent));
        _producer.Send(payload).AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await _producer.DisposeAsync().ConfigureAwait(false);
        if (_ownsClient && _client is not null)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
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
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
