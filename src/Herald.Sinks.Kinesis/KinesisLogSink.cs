// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.Kinesis;
using Amazon.Kinesis.Model;
using MMP.Herald;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.Kinesis;

/// <summary>
/// Sink that writes log events as records to an AWS Kinesis Data Stream
/// via the AWS SDK. Drop-in equivalent for Serilog.Sinks.AmazonKinesis.
/// </summary>
/// <remarks>
/// <para>
/// <b>Partition key.</b> Defaults to the event category so related
/// events stick to a shard for ordered consumption. Pass a custom
/// <c>partitionKeyAccessor</c> for tenant- or trace-id-based shard
/// affinity.
/// </para>
/// <para>
/// <b>Batch limits.</b> Kinesis PutRecords accepts up to 500 records
/// or 5 MB per call. The sink chunks larger batches at 500 records
/// per request automatically.
/// </para>
/// </remarks>
public sealed class KinesisLogSink : ILogger, IBatchedLogSink, IDisposable
{
    private const int PutRecordsLimit = 500;

    private readonly IAmazonKinesis _client;
    private readonly string _streamName;
    private readonly Func<LogEvent, string>? _partitionKeyAccessor;
    private readonly bool _ownsClient;

    public KinesisLogSink(string streamName, RegionEndpoint region, Func<LogEvent, string>? partitionKeyAccessor = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentNullException.ThrowIfNull(region);

        _streamName = streamName;
        _client = new AmazonKinesisClient(region);
        _partitionKeyAccessor = partitionKeyAccessor;
        _ownsClient = true;
    }

    public KinesisLogSink(IAmazonKinesis client, string streamName, Func<LogEvent, string>? partitionKeyAccessor = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);

        _client = client;
        _streamName = streamName;
        _partitionKeyAccessor = partitionKeyAccessor;
        _ownsClient = false;
    }

    public void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        var data = SerializeEvent(logEvent);
        var request = new PutRecordRequest
        {
            StreamName = _streamName,
            PartitionKey = ResolveKey(logEvent),
            Data = new MemoryStream(Encoding.UTF8.GetBytes(data)),
        };
        _client.PutRecordAsync(request).GetAwaiter().GetResult();
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        for (int offset = 0; offset < events.Count; offset += PutRecordsLimit)
        {
            var slice = new List<PutRecordsRequestEntry>(Math.Min(PutRecordsLimit, events.Count - offset));
            for (int i = offset; i < Math.Min(offset + PutRecordsLimit, events.Count); i++)
            {
                var evt = events[i];
                slice.Add(new PutRecordsRequestEntry
                {
                    PartitionKey = ResolveKey(evt),
                    Data = new MemoryStream(Encoding.UTF8.GetBytes(SerializeEvent(evt))),
                });
            }
            _client.PutRecordsAsync(new PutRecordsRequest { StreamName = _streamName, Records = slice })
                   .GetAwaiter().GetResult();
        }
    }

    public void Dispose()
    {
        if (_ownsClient) _client.Dispose();
    }

    private string ResolveKey(LogEvent evt) =>
        _partitionKeyAccessor?.Invoke(evt) ?? evt.Category.Value;

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
