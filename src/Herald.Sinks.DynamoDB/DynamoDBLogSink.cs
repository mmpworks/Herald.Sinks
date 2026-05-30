// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.DynamoDB;

/// <summary>
/// Sink that writes log events as items to a DynamoDB table via the
/// AWS SDK. Drop-in equivalent for Serilog.Sinks.AmazonDynamoDB.
/// </summary>
/// <remarks>
/// <para>
/// <b>Item shape.</b> Each event becomes a flat item with attributes
/// <c>id</c> (S, partition key — date#tick#rand), <c>time_utc</c> (S),
/// <c>level</c> (S), <c>category</c> (S), <c>message</c> (S),
/// <c>template</c> (S), and <c>properties</c> (S, JSON).
/// </para>
/// <para>
/// <b>Auth.</b> The default-credentials ctor uses the AWS SDK's standard
/// credential chain (env vars → shared credentials → instance profile).
/// Code-first overload accepts a pre-built <see cref="IAmazonDynamoDB"/>
/// for app-level credential sharing.
/// </para>
/// <para>
/// <b>Batch limits.</b> DynamoDB BatchWriteItem caps at 25 items per
/// call; the sink chunks larger batches automatically.
/// </para>
/// </remarks>
public sealed class DynamoDBLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable, INetworkSink
{
    private const int BatchWriteLimit = 25;

    private readonly IAmazonDynamoDB _client;
    private readonly string _tableName;
    private readonly bool _ownsClient;
    private readonly Random _suffix = new();

    public DynamoDBLogSink(string tableName, RegionEndpoint region)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentNullException.ThrowIfNull(region);

        _tableName = tableName;
        _client = new AmazonDynamoDBClient(region);
        _ownsClient = true;
    }

    /// <summary>
    /// Code-first overload accepts a pre-built <see cref="IAmazonDynamoDB"/>
    /// — typical when the app already shares an SDK client.
    /// </summary>
    public DynamoDBLogSink(IAmazonDynamoDB client, string tableName)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        _client = client;
        _tableName = tableName;
        _ownsClient = false;
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        var request = new PutItemRequest(_tableName, BuildItem(logEvent));
        _client.PutItemAsync(request).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        // BatchWriteItem caps at 25 items. Chunk the input so a 1000-event
        // batch issues 40 requests instead of throwing.
        for (int offset = 0; offset < events.Count; offset += BatchWriteLimit)
        {
            var slice = new List<WriteRequest>(Math.Min(BatchWriteLimit, events.Count - offset));
            for (int i = offset; i < Math.Min(offset + BatchWriteLimit, events.Count); i++)
            {
                slice.Add(new WriteRequest(new PutRequest(BuildItem(events[i]))));
            }
            var request = new BatchWriteItemRequest(new Dictionary<string, List<WriteRequest>> { [_tableName] = slice });
            _client.BatchWriteItemAsync(request).ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }

    private Dictionary<string, AttributeValue> BuildItem(LogEvent evt)
    {
        // Composite id sorts newest-first within a date prefix so range
        // scans stay efficient. The random suffix avoids collisions on
        // bursts that share the same tick.
        var id = $"{evt.TimeUtc.UtcDateTime:yyyyMMdd}#{long.MaxValue - evt.TimeUtc.UtcTicks:D19}#{_suffix.Next():X8}";

        var item = new Dictionary<string, AttributeValue>
        {
            ["id"] = new AttributeValue { S = id },
            ["time_utc"] = new AttributeValue { S = evt.TimeUtc.UtcDateTime.ToString("O") },
            ["level"] = new AttributeValue { S = evt.Level.Key },
            ["category"] = new AttributeValue { S = evt.Category.Value },
            ["message"] = new AttributeValue { S = evt.Message ?? string.Empty },
            ["template"] = new AttributeValue { S = evt.MessageTemplate ?? string.Empty },
        };

        if (evt.Properties is not null && evt.Properties.Count > 0)
        {
            item["properties"] = new AttributeValue { S = SerializeProperties(evt) };
        }

        return item;
    }

    private static string SerializeProperties(LogEvent evt)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var prop in evt.Properties)
            {
                WriteJsonValue(writer, prop.Name, prop.ResolvedValue);
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
