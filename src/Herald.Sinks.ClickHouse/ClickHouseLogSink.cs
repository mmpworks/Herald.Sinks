// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Data;
using ClickHouse.Client.ADO;
using ClickHouse.Client.Copy;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.ClickHouse;

/// <summary>
/// Sink that inserts log events into a ClickHouse table via the
/// ClickHouse.Client driver's bulk-copy interface. ClickHouse is a
/// columnar OLAP store; this sink targets it as a logs warehouse.
/// </summary>
/// <remarks>
/// <para>
/// <b>Schema.</b> The sink expects a table with at least these columns
/// (any DateTime / Enum / String / Map flavour works):
/// <c>time_utc DateTime64(3, 'UTC')</c>,
/// <c>level LowCardinality(String)</c>,
/// <c>category LowCardinality(String)</c>,
/// <c>message String</c>,
/// <c>template String</c>.
/// Tuned suggestion: <c>ENGINE = MergeTree ORDER BY (toDate(time_utc), level)</c>
/// for cheap range scans plus level-aware filtering.
/// </para>
/// <para>
/// <b>Bulk copy.</b> The sink uses ClickHouseBulkCopy for batched
/// inserts — same path the official driver recommends for sustained
/// throughput. Single events still flow through ADO.NET INSERTs.
/// </para>
/// </remarks>
public sealed class ClickHouseLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable, INetworkSink
{
    private readonly string _connectionString;
    private readonly string _tableName;

    public ClickHouseLogSink(string connectionString, string tableName = "herald_logs")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        if (!IsValidIdentifier(tableName))
        {
            throw new ArgumentException(
                $"Invalid table name '{tableName}'. ClickHouse identifiers must match [a-zA-Z_][a-zA-Z0-9_]*.",
                nameof(tableName));
        }

        _connectionString = connectionString;
        _tableName = tableName;
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        using var connection = new ClickHouseConnection(_connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"INSERT INTO {_tableName} (time_utc, level, category, message, template) VALUES ({{time:DateTime64(3, 'UTC')}}, {{level:String}}, {{category:String}}, {{message:String}}, {{template:String}})";
        AddParam(cmd, "time", logEvent.TimeUtc.UtcDateTime);
        AddParam(cmd, "level", logEvent.Level.Key);
        AddParam(cmd, "category", logEvent.Category.Value);
        AddParam(cmd, "message", logEvent.Message ?? string.Empty);
        AddParam(cmd, "template", logEvent.MessageTemplate ?? string.Empty);
        cmd.ExecuteNonQuery();
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        // ClickHouseBulkCopy is the canonical batch-insert path for the
        // driver. It ships a single HTTP request with the whole batch
        // encoded as a native column block — much faster than per-row
        // INSERTs at any meaningful volume.
        using var connection = new ClickHouseConnection(_connectionString);
        connection.Open();

        using var bulk = new ClickHouseBulkCopy(connection)
        {
            DestinationTableName = _tableName,
            ColumnNames = new[] { "time_utc", "level", "category", "message", "template" },
        };

        var rows = new List<object[]>(events.Count);
        foreach (var evt in events)
        {
            rows.Add(new object[]
            {
                evt.TimeUtc.UtcDateTime,
                evt.Level.Key,
                evt.Category.Value,
                evt.Message ?? string.Empty,
                evt.MessageTemplate ?? string.Empty,
            });
        }

        bulk.InitAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        bulk.WriteToServerAsync(rows).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public void Dispose() { /* nothing owned */ }

    private static void AddParam(IDbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static bool IsValidIdentifier(string name) =>
        System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
}
