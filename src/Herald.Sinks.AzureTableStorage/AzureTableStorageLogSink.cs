// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Azure.Core;
using Azure.Data.Tables;
using Azure.Identity;
using MMP.Herald;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.AzureTableStorage;

/// <summary>
/// Partition-key strategy for <see cref="AzureTableStorageLogSink"/>.
/// </summary>
/// <remarks>
/// The strategy controls how events cluster inside the table. Picking
/// the right one matters for read cost — Azure Tables is fastest when
/// a query hits a single partition.
/// <list type="bullet">
///   <item><b>UtcDay</b> — one partition per day. Good default for
///     mid-volume services; day-scoped queries stay in one partition.</item>
///   <item><b>UtcHour</b> — one partition per hour. Use when write
///     volume is high enough that daily partitions get too large.</item>
///   <item><b>UtcMinute</b> — one partition per minute. Very high
///     volume; trades query locality for ingestion parallelism.</item>
///   <item><b>Fixed</b> — one partition for the whole table. Cheapest
///     writes, worst reads; fine for low-volume archival.</item>
/// </list>
/// </remarks>
public enum AzureTablePartitionKeyStrategy
{
    UtcDay,
    UtcHour,
    UtcMinute,
    Fixed
}

/// <summary>
/// Sink that writes log events as entities into an Azure Table Storage
/// table via the <see cref="TableClient"/> SDK. Cheap high-volume
/// archival destination for Azure-hosted workloads.
/// </summary>
/// <remarks>
/// <para>
/// <b>Schema.</b> Each event becomes one entity:
/// </para>
/// <list type="bullet">
///   <item><c>PartitionKey</c> — from
///     <see cref="AzureTablePartitionKeyStrategy"/>.</item>
///   <item><c>RowKey</c> — reverse-tick timestamp plus a sequence
///     suffix so newest entries sort first on range scans.</item>
///   <item><c>Timestamp</c> — Azure Tables assigns this automatically;
///     we also write the event's own <c>TimeUtc</c> as
///     <c>EventTimeUtc</c> so timestamp fidelity survives the round
///     trip.</item>
///   <item><c>Level</c>, <c>Category</c>, <c>Message</c>,
///     <c>MessageTemplate</c>.</item>
///   <item><c>Exception</c> — exception.ToString when present in the
///     event's context under the standard exception key.</item>
///   <item>One column per Herald property and context value
///     (name-sanitised to Table Storage's column-name rules).</item>
/// </list>
/// <para>
/// Properties with simple types (string, number, bool, DateTime, Guid,
/// byte[]) land as native Table columns. Everything else is
/// stringified. Property names that fail the Table column-name regex
/// are sanitised character-by-character so the same Herald property
/// always maps to the same column.
/// </para>
/// <para>
/// <b>Auth.</b> Three constructors cover the typical wire-ups:
/// connection-string (shared-key), DefaultAzureCredential
/// (managed-identity), or pre-built <see cref="TableClient"/>
/// (full caller control).
/// </para>
/// </remarks>
public sealed class AzureTableStorageLogSink : ILogger, IBatchedLogSink
{
    private const string DefaultFixedPartitionKey = "logs";

    private readonly TableClient _tableClient;
    private readonly AzureTablePartitionKeyStrategy _partitionStrategy;
    private readonly string _fixedPartitionKey;
    private long _rowSequence;

    /// <summary>
    /// Construct from a pre-built <see cref="TableClient"/>. Preferred
    /// for production wire-ups: the caller owns auth (connection
    /// string, managed identity, SAS) and table lifecycle.
    /// </summary>
    public AzureTableStorageLogSink(
        TableClient tableClient,
        AzureTablePartitionKeyStrategy partitionStrategy = AzureTablePartitionKeyStrategy.UtcDay,
        string fixedPartitionKey = DefaultFixedPartitionKey)
    {
        ArgumentNullException.ThrowIfNull(tableClient);
        _tableClient = tableClient;
        _partitionStrategy = partitionStrategy;
        _fixedPartitionKey = string.IsNullOrWhiteSpace(fixedPartitionKey)
            ? DefaultFixedPartitionKey
            : fixedPartitionKey;
    }

    /// <summary>
    /// Connection-string overload. Creates its own
    /// <see cref="TableClient"/> + ensures the table exists.
    /// </summary>
    public AzureTableStorageLogSink(
        string connectionString,
        string tableName = "HeraldLogs",
        AzureTablePartitionKeyStrategy partitionStrategy = AzureTablePartitionKeyStrategy.UtcDay,
        string fixedPartitionKey = DefaultFixedPartitionKey)
        : this(BuildClientFromConnectionString(connectionString, tableName), partitionStrategy, fixedPartitionKey)
    {
    }

    /// <summary>
    /// Managed-identity overload. <paramref name="useDefaultCredential"/>
    /// must be true to confirm intent — passing false throws so a
    /// misconfigured call site fails fast at startup rather than
    /// running against an anonymous client at runtime.
    /// </summary>
    public AzureTableStorageLogSink(
        string accountUri,
        string tableName,
        bool useDefaultCredential,
        AzureTablePartitionKeyStrategy partitionStrategy = AzureTablePartitionKeyStrategy.UtcDay,
        string fixedPartitionKey = DefaultFixedPartitionKey)
        : this(BuildClientFromManagedIdentity(accountUri, tableName, useDefaultCredential), partitionStrategy, fixedPartitionKey)
    {
    }

    private static TableClient BuildClientFromConnectionString(string connectionString, string tableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        var client = new TableClient(connectionString, tableName);
        client.CreateIfNotExists();
        return client;
    }

    private static TableClient BuildClientFromManagedIdentity(
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

        var credential = (TokenCredential)new DefaultAzureCredential();
        var client = new TableClient(new Uri(accountUri), tableName, credential);
        client.CreateIfNotExists();
        return client;
    }

    public void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        _tableClient.AddEntity(BuildEntity(logEvent));
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        // Azure Tables transactions are scoped to one partition and
        // capped at 100 entities. Group events by partition key and
        // flush each group in batches of 100. Under the common UtcDay
        // strategy this collapses to one transaction per 100 events.
        var groups = new Dictionary<string, List<TableTransactionAction>>();
        foreach (var evt in events)
        {
            var entity = BuildEntity(evt);
            if (!groups.TryGetValue(entity.PartitionKey, out var actions))
            {
                actions = new List<TableTransactionAction>();
                groups[entity.PartitionKey] = actions;
            }
            actions.Add(new TableTransactionAction(TableTransactionActionType.Add, entity));
        }

        foreach (var kv in groups)
        {
            for (var offset = 0; offset < kv.Value.Count; offset += 100)
            {
                var slice = kv.Value.GetRange(offset, Math.Min(100, kv.Value.Count - offset));
                _tableClient.SubmitTransaction(slice);
            }
        }
    }

    // ── Entity construction ──────────────────────────────────────
    //
    // BuildEntity / BuildPartitionKey / BuildRowKey / SanitizeColumnName
    // are internal so the test suite can exercise the entity layout
    // without spinning up the Azurite emulator. InternalsVisibleTo for
    // Herald.Sinks.AzureTableStorage.Tests lives in the csproj.

    internal TableEntity BuildEntity(LogEvent evt)
    {
        var entity = new TableEntity(
            BuildPartitionKey(evt.TimeUtc),
            BuildRowKey(evt.TimeUtc))
        {
            ["EventTimeUtc"] = evt.TimeUtc,
            ["Level"] = evt.Level.Key,
            ["Category"] = evt.Category.Value,
            ["Message"] = evt.Message,
            ["MessageTemplate"] = evt.MessageTemplate
        };

        if (evt.Context.TryGetValue(LogContextKeys.Exception, out var exValue)
            && exValue is Exception ex)
        {
            entity["Exception"] = ex.ToString();
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PartitionKey", "RowKey", "Timestamp", "ETag",
            "EventTimeUtc", "Level", "Category", "Message",
            "MessageTemplate", "Exception"
        };

        foreach (var property in evt.Properties)
        {
            var column = SanitizeColumnName(property.Name);
            if (seen.Add(column))
            {
                entity[column] = ToTableValue(property.ResolvedValue);
            }
        }

        foreach (var pair in evt.Context)
        {
            if (pair.Key == LogContextKeys.Exception) continue;

            var column = SanitizeColumnName(pair.Key);
            if (seen.Add(column))
            {
                entity[column] = ToTableValue(pair.Value);
            }
        }

        return entity;
    }

    internal string BuildPartitionKey(DateTimeOffset time) => _partitionStrategy switch
    {
        AzureTablePartitionKeyStrategy.UtcDay => time.UtcDateTime.ToString("yyyy-MM-dd"),
        AzureTablePartitionKeyStrategy.UtcHour => time.UtcDateTime.ToString("yyyy-MM-dd-HH"),
        AzureTablePartitionKeyStrategy.UtcMinute => time.UtcDateTime.ToString("yyyy-MM-dd-HH-mm"),
        _ => _fixedPartitionKey
    };

    internal string BuildRowKey(DateTimeOffset time)
    {
        // Reverse-tick so newest entries appear first on a range scan.
        // A per-sink sequence suffix breaks ties for events arriving in
        // the same tick (common under load).
        var reverseTick = (long.MaxValue - time.UtcTicks).ToString("D19");
        var sequence = Interlocked.Increment(ref _rowSequence).ToString("D10");
        return $"{reverseTick}-{sequence}";
    }

    private static object ToTableValue(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        bool b => b,
        int i => i,
        long l => l,
        double d => d,
        float f => (double)f,
        decimal m => (double)m,
        DateTime dt => dt,
        DateTimeOffset dto => dto,
        Guid g => g,
        byte[] bytes => bytes,
        _ => value.ToString() ?? string.Empty
    };

    internal static string SanitizeColumnName(string name)
    {
        // Table Storage columns must match [A-Za-z_][A-Za-z0-9_]{0,254}.
        // Replace anything else with underscore; prefix with underscore
        // if the first char is a digit. Cheap and deterministic so the
        // same Herald property always lands in the same column.
        if (string.IsNullOrEmpty(name)) return "unnamed";

        Span<char> buffer = stackalloc char[Math.Min(name.Length, 255)];
        var length = 0;
        for (var i = 0; i < name.Length && length < buffer.Length; i++)
        {
            var c = name[i];
            var valid = (c >= 'A' && c <= 'Z')
                     || (c >= 'a' && c <= 'z')
                     || (c >= '0' && c <= '9')
                     || c == '_';
            buffer[length++] = valid ? c : '_';
        }

        if (length == 0) return "unnamed";
        if (buffer[0] >= '0' && buffer[0] <= '9')
        {
            // Prepend an underscore — simplest way to satisfy the "must
            // not start with a digit" rule without mutating earlier chars.
            return "_" + new string(buffer[..length]);
        }
        return new string(buffer[..length]);
    }
}
