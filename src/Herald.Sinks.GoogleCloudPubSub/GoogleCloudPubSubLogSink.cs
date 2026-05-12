// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using MMP.Herald;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.GoogleCloudPubSub;

/// <summary>
/// Sink that publishes log events to a Google Cloud Pub/Sub topic via
/// the official SDK. <see cref="PublisherClient"/> handles internal
/// batching, retries, and ordering by default.
/// </summary>
/// <remarks>
/// The connection-string ctor builds and owns a PublisherClient with
/// SDK defaults (Application Default Credentials chain). Apps that
/// already share a publisher pass it in via the code-first overload.
/// </remarks>
public sealed class GoogleCloudPubSubLogSink : ILogger, IBatchedLogSink, IDisposable
{
    private readonly PublisherClient _publisher;
    private readonly bool _ownsPublisher;

    public GoogleCloudPubSubLogSink(string projectId, string topicId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(topicId);

        var topicName = TopicName.FromProjectTopic(projectId, topicId);
        _publisher = PublisherClient.Create(topicName);
        _ownsPublisher = true;
    }

    public GoogleCloudPubSubLogSink(PublisherClient publisher)
    {
        ArgumentNullException.ThrowIfNull(publisher);
        _publisher = publisher;
        _ownsPublisher = false;
    }

    public void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        var message = new PubsubMessage
        {
            Data = ByteString.CopyFromUtf8(SerializeEvent(logEvent)),
        };
        message.Attributes["level"] = logEvent.Level.Key;
        message.Attributes["category"] = logEvent.Category.Value;
        _publisher.PublishAsync(message).GetAwaiter().GetResult();
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        // PublisherClient batches internally; per-event PublishAsync calls
        // accumulate into a batch driven by max-bytes / max-messages /
        // max-time settings on the publisher. Awaiting all of them here
        // preserves ordering semantics with respect to the caller.
        var tasks = new List<System.Threading.Tasks.Task<string>>(events.Count);
        foreach (var evt in events)
        {
            var message = new PubsubMessage
            {
                Data = ByteString.CopyFromUtf8(SerializeEvent(evt)),
            };
            message.Attributes["level"] = evt.Level.Key;
            message.Attributes["category"] = evt.Category.Value;
            tasks.Add(_publisher.PublishAsync(message));
        }
        System.Threading.Tasks.Task.WhenAll(tasks).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        if (_ownsPublisher)
        {
            // ShutdownAsync flushes pending publish requests with a 30s
            // window before tearing down channels.
            try { _publisher.ShutdownAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult(); }
            catch (Exception) { /* best-effort on shutdown */ }
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
