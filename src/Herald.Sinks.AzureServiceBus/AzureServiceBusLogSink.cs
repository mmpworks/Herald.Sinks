// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.AzureServiceBus;

/// <summary>
/// Sink that sends log events as messages into an Azure Service Bus
/// queue or topic via the official SDK.
/// </summary>
/// <remarks>
/// <b>Sender lifetime.</b> The connection-string ctor builds and owns
/// a <see cref="ServiceBusClient"/> + <see cref="ServiceBusSender"/>.
/// Apps already using ServiceBusClient share via the code-first
/// overload that accepts a sender directly.
/// </remarks>
public sealed class AzureServiceBusLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable, INetworkSink
{
    private readonly ServiceBusSender _sender;
    private readonly ServiceBusClient? _ownedClient;

    public AzureServiceBusLogSink(string connectionString, string queueOrTopic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueOrTopic);

        _ownedClient = new ServiceBusClient(connectionString);
        _sender = _ownedClient.CreateSender(queueOrTopic);
    }

    public AzureServiceBusLogSink(ServiceBusSender sender)
    {
        ArgumentNullException.ThrowIfNull(sender);
        _sender = sender;
        _ownedClient = null;
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        var message = new ServiceBusMessage(SerializeEvent(logEvent))
        {
            ContentType = "application/json",
            Subject = logEvent.Level.Key,
        };
        _sender.SendMessageAsync(message).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        var batch = new List<ServiceBusMessage>(events.Count);
        foreach (var evt in events)
        {
            batch.Add(new ServiceBusMessage(SerializeEvent(evt))
            {
                ContentType = "application/json",
                Subject = evt.Level.Key,
            });
        }
        _sender.SendMessagesAsync(batch).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        if (_ownedClient is not null)
        {
            _sender.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
            _ownedClient.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
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
        return Encoding.UTF8.GetString(stream.ToArray());
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
