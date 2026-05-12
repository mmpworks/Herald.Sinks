// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Azure.Data.Tables;
using Azure.Identity;
using MMP.Herald;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.AzureTableStorage;

/// <summary>
/// Sink that writes log events as entities into an Azure Table Storage
/// table via the <see cref="TableClient"/> SDK. Drop-in equivalent for
/// Serilog.Sinks.AzureTableStorage.
/// </summary>
/// <remarks>
/// <para>
/// <b>Partition key.</b> Uses the event's UTC date (yyyyMMdd) so daily
/// partitions stay balanced and date-range queries scan a bounded
/// number of partitions. Row key uses inverted ticks plus a random
/// suffix so newest events sort first within a partition.
/// </para>
/// <para>
/// <b>Property serialisation.</b> Entity columns are limited to
/// primitives by Azure Tables; complex property bags ride along as a
/// JSON-encoded <c>Properties</c> column.
/// </para>
/// <para>
/// <b>Auth.</b> Connection-string ctor for shared-key access;
/// DefaultAzureCredential overload for managed-identity scenarios.
/// </para>
/// </remarks>
public sealed class AzureTableStorageLogSink : ILogger, IBatchedLogSink
{
    private readonly TableClient _table;
    private readonly Random _suffix = new();

    public AzureTableStorageLogSink(string connectionString, string tableName = "HeraldLogs")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        _table = new TableClient(connectionString, tableName);
        _table.CreateIfNotExists();
    }

    /// <summary>
    /// Managed-identity overload. <paramref name="useDefaultCredential"/>
    /// must be true to confirm intent — passing false throws so a
    /// misconfigured callsite fails fast at startup rather than running
    /// against an anonymous client at runtime.
    /// </summary>
    public AzureTableStorageLogSink(
        string accountUri,
        string tableName,
        bool useDefaultCredential)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        if (!useDefaultCredential)
        {
            throw new ArgumentException(
                "useDefaultCredential must be true on this overload. " +
                "For shared-key auth use the connection-string ctor.",
                nameof(useDefaultCredential));
        }

        _table = new TableClient(new Uri(accountUri), tableName, new DefaultAzureCredential());
        _table.CreateIfNotExists();
    }

    /// <summary>
    /// Code-first overload accepts a pre-built <see cref="TableClient"/>
    /// — typical when the app already shares a TableServiceClient with
    /// other components.
    /// </summary>
    public AzureTableStorageLogSink(TableClient tableClient)
    {
        ArgumentNullException.ThrowIfNull(tableClient);
        _table = tableClient;
    }

    public void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        _table.AddEntity(BuildEntity(logEvent));
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        // Azure Tables transactions are bounded to a single partition and
        // ≤ 100 entities. Group by partition key (date), flush each group
        // as a transactional insert, falling back to per-entity writes
        // for groups larger than the 100-entity ceiling.
        var byPartition = new Dictionary<string, List<TableTransactionAction>>();
        foreach (var evt in events)
        {
            var entity = BuildEntity(evt);
            if (!byPartition.TryGetValue(entity.PartitionKey, out var bucket))
            {
                bucket = new List<TableTransactionAction>();
                byPartition[entity.PartitionKey] = bucket;
            }
            bucket.Add(new TableTransactionAction(TableTransactionActionType.Add, entity));
        }

        foreach (var (_, bucket) in byPartition)
        {
            for (int offset = 0; offset < bucket.Count; offset += 100)
            {
                var slice = bucket.GetRange(offset, Math.Min(100, bucket.Count - offset));
                _table.SubmitTransaction(slice);
            }
        }
    }

    private TableEntity BuildEntity(LogEvent evt)
    {
        var partitionKey = evt.TimeUtc.UtcDateTime.ToString("yyyyMMdd");
        // Inverted ticks puts newest first; random suffix avoids collisions
        // on bursts that share the same tick.
        var rowKey = $"{long.MaxValue - evt.TimeUtc.UtcTicks:D19}-{_suffix.Next():X8}";

        var entity = new TableEntity(partitionKey, rowKey)
        {
            ["TimeUtc"] = evt.TimeUtc.UtcDateTime,
            ["Level"] = evt.Level.Key,
            ["Category"] = evt.Category.Value,
            ["Message"] = evt.Message ?? string.Empty,
            ["Template"] = evt.MessageTemplate ?? string.Empty,
        };

        if (evt.Properties is not null && evt.Properties.Count > 0)
        {
            entity["Properties"] = SerializeProperties(evt);
        }

        return entity;
    }

    private static string SerializeProperties(LogEvent evt)
    {
        // Utf8JsonWriter keeps the path AOT-clean.
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
