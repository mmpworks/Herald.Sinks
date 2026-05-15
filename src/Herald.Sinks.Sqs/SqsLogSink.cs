// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.Sqs;

/// <summary>
/// Sink that writes log events as messages to an AWS SQS queue via the
/// AWS SDK. Drop-in equivalent for Serilog.Sinks.AmazonSqs.
/// </summary>
/// <remarks>
/// SQS SendMessageBatch caps at 10 messages per call. The sink chunks
/// larger batches into 10-message slices automatically.
/// </remarks>
public sealed class SqsLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable, INetworkSink
{
    private const int SendBatchLimit = 10;

    private readonly IAmazonSQS _client;
    private readonly string _queueUrl;
    private readonly bool _ownsClient;

    public SqsLogSink(string queueUrl, RegionEndpoint region)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueUrl);
        ArgumentNullException.ThrowIfNull(region);

        _queueUrl = queueUrl;
        _client = new AmazonSQSClient(region);
        _ownsClient = true;
    }

    public SqsLogSink(IAmazonSQS client, string queueUrl)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueUrl);

        _client = client;
        _queueUrl = queueUrl;
        _ownsClient = false;
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        var request = new SendMessageRequest
        {
            QueueUrl = _queueUrl,
            MessageBody = SerializeEvent(logEvent),
        };
        _client.SendMessageAsync(request).GetAwaiter().GetResult();
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        for (int offset = 0; offset < events.Count; offset += SendBatchLimit)
        {
            var entries = new List<SendMessageBatchRequestEntry>(Math.Min(SendBatchLimit, events.Count - offset));
            int idLocal = 0;
            for (int i = offset; i < Math.Min(offset + SendBatchLimit, events.Count); i++)
            {
                entries.Add(new SendMessageBatchRequestEntry
                {
                    Id = $"e{idLocal++}",
                    MessageBody = SerializeEvent(events[i]),
                });
            }
            _client.SendMessageBatchAsync(new SendMessageBatchRequest
            {
                QueueUrl = _queueUrl,
                Entries = entries,
            }).GetAwaiter().GetResult();
        }
    }

    public void Dispose()
    {
        if (_ownsClient) _client.Dispose();
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
