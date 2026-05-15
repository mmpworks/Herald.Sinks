// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.AzureEventHub;

/// <summary>
/// Sink that publishes Herald log events as messages to an Azure
/// Event Hubs partition via <see cref="EventHubProducerClient"/>.
/// Drop-in for Serilog.Sinks.AzureEventHub.
/// </summary>
/// <remarks>
/// <para>
/// <b>Auth.</b> Two paths: connection string (key embedded) or
/// DefaultAzureCredential against the fully-qualified namespace.
/// Production typically prefers the credential path with a managed
/// identity.
/// </para>
/// <para>
/// <b>Batching.</b> One LogBatch becomes one Event Hubs SendAsync —
/// the SDK builds an <c>EventDataBatch</c> internally so the network
/// round-trip count matches Herald's batching cadence.
/// </para>
/// </remarks>
public sealed class AzureEventHubLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable, INetworkSink
{
    private readonly EventHubProducerClient _producer;
    private readonly bool _ownsProducer;

    public AzureEventHubLogSink(string connectionString, string eventHubName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventHubName);

        _producer = new EventHubProducerClient(connectionString, eventHubName);
        _ownsProducer = true;
    }

    /// <summary>
    /// DefaultAzureCredential overload — for managed-identity / SSO
    /// auth against the fully-qualified namespace
    /// (e.g. <c>my-ns.servicebus.windows.net</c>).
    /// </summary>
    public AzureEventHubLogSink(string fullyQualifiedNamespace, string eventHubName, bool useDefaultCredential)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullyQualifiedNamespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventHubName);
        if (!useDefaultCredential)
            throw new ArgumentException("This overload is for DefaultAzureCredential. Pass true.", nameof(useDefaultCredential));

        _producer = new EventHubProducerClient(fullyQualifiedNamespace, eventHubName, new DefaultAzureCredential());
        _ownsProducer = true;
    }

    /// <summary>
    /// Code-first overload accepting a pre-built producer.
    /// </summary>
    public AzureEventHubLogSink(EventHubProducerClient producer)
    {
        ArgumentNullException.ThrowIfNull(producer);
        _producer = producer;
        _ownsProducer = false;
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        LogBatch(new[] { logEvent });
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        // CreateBatch + TryAdd is the safe path — Event Hubs caps each
        // batch at 1 MB and TryAdd returns false when adding would
        // overflow. We send the partial batch and start a new one
        // rather than dropping the rest.
        var batch = _producer.CreateBatchAsync().GetAwaiter().GetResult();

        foreach (var evt in events)
        {
            var data = new EventData(BuildBody(evt))
            {
                ContentType = "application/json",
            };
            data.Properties["level"] = evt.Level.Key;
            data.Properties["category"] = evt.Category.Value;

            if (!batch.TryAdd(data))
            {
                _producer.SendAsync(batch).GetAwaiter().GetResult();
                batch.Dispose();
                batch = _producer.CreateBatchAsync().GetAwaiter().GetResult();
                if (!batch.TryAdd(data))
                {
                    // Single oversize event — skip it rather than fail
                    // the whole batch. Event Hubs body is capped at 1 MB.
                    continue;
                }
            }
        }

        if (batch.Count > 0)
        {
            _producer.SendAsync(batch).GetAwaiter().GetResult();
        }
        batch.Dispose();
    }

    public void Dispose()
    {
        if (_ownsProducer) _producer.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private static byte[] BuildBody(LogEvent evt)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("time_utc", evt.TimeUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("level", evt.Level.Key);
            writer.WriteString("category", evt.Category.Value);
            writer.WriteString("message", evt.Message ?? string.Empty);
            writer.WriteString("template", evt.MessageTemplate ?? string.Empty);

            if (evt.Context.TryGetValue(LogContextKeys.Exception, out var v) && v is Exception ex)
                writer.WriteString("exception", ex.ToString());

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
