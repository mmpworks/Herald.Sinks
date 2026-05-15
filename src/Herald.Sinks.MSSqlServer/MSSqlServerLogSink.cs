// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;

namespace Herald.Sinks.MSSqlServer;

/// <summary>
/// Sink that writes log events as rows in a Microsoft SQL Server or
/// Azure SQL table. Parameterized INSERT for single-event <c>Log</c>
/// calls, <see cref="SqlBulkCopy"/> for batches via
/// <see cref="IBatchedLogSink"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Schema.</b> See <see cref="MSSqlServerColumnOptions"/> remarks for
/// the column shape the sink expects. The sink does not write
/// <c>Id</c> — the database supplies it via IDENTITY.
/// </para>
/// <para>
/// <b>Auto-create.</b> When <c>autoCreateTable</c> is true the sink
/// issues <c>CREATE TABLE IF NOT EXISTS</c>-style DDL on first write.
/// Preferred shape in production is installer-managed schema — the
/// flag exists for dev loops and tests.
/// </para>
/// <para>
/// <b>Thread safety.</b> The sink is thread-safe. Each call opens a
/// fresh pooled connection; the Microsoft.Data.SqlClient connection
/// pool handles concurrency.
/// </para>
/// </remarks>
public sealed class MSSqlServerLogSink : HeraldSinkBase, IBatchedLogSink, INetworkSink
{
    private readonly string _connectionString;
    private readonly string _schemaName;
    private readonly string _tableName;
    private readonly MSSqlServerColumnOptions _columns;
    private readonly bool _autoCreateTable;
    private int _tableEnsured;

    public MSSqlServerLogSink(
        string connectionString,
        string tableName = "Logs",
        string schemaName = "dbo",
        MSSqlServerColumnOptions? columns = null,
        bool autoCreateTable = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);

        _connectionString = connectionString;
        _tableName = tableName;
        _schemaName = schemaName;
        _columns = columns ?? MSSqlServerColumnOptions.Default;
        _autoCreateTable = autoCreateTable;
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        EnsureTableOnce();

        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var command = BuildInsertCommand(connection, logEvent);
        command.ExecuteNonQuery();
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;
        EnsureTableOnce();

        // One connection, one bulk-copy — the fast path for sustained
        // writes. SqlBulkCopy skips the full INSERT protocol and streams
        // rows through the tabular data stream.
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var bulk = new SqlBulkCopy(connection)
        {
            DestinationTableName = FullyQualifiedTable(),
        };

        MapColumns(bulk);

        using var table = BuildDataTable();
        foreach (var evt in events)
        {
            AddRow(table, evt);
        }

        bulk.WriteToServer(table);
    }

    private SqlCommand BuildInsertCommand(SqlConnection connection, LogEvent evt)
    {
        var sql =
            $"INSERT INTO {FullyQualifiedTable()} " +
            $"([{_columns.TimeUtc}], [{_columns.Level}], [{_columns.Category}], " +
            $"[{_columns.Message}], [{_columns.Template}], [{_columns.Exception}], " +
            $"[{_columns.Properties}]) " +
            $"VALUES (@time, @level, @category, @message, @template, @exception, @properties);";

        var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@time", SqlDbType.DateTime2).Value = evt.TimeUtc.UtcDateTime;
        command.Parameters.Add("@level", SqlDbType.VarChar, 32).Value = evt.Level.Key;
        command.Parameters.Add("@category", SqlDbType.VarChar, 128).Value = evt.Category.Value;
        command.Parameters.Add("@message", SqlDbType.NVarChar, -1).Value = evt.Message;
        command.Parameters.Add("@template", SqlDbType.NVarChar, -1).Value = evt.MessageTemplate;
        command.Parameters.Add("@exception", SqlDbType.NVarChar, -1).Value = (object?)GetExceptionText(evt) ?? DBNull.Value;
        command.Parameters.Add("@properties", SqlDbType.NVarChar, -1).Value = (object?)GetPropertiesJson(evt) ?? DBNull.Value;
        return command;
    }

    private DataTable BuildDataTable()
    {
        var table = new DataTable();
        table.Columns.Add(_columns.TimeUtc, typeof(DateTime));
        table.Columns.Add(_columns.Level, typeof(string));
        table.Columns.Add(_columns.Category, typeof(string));
        table.Columns.Add(_columns.Message, typeof(string));
        table.Columns.Add(_columns.Template, typeof(string));
        table.Columns.Add(_columns.Exception, typeof(string)).AllowDBNull = true;
        table.Columns.Add(_columns.Properties, typeof(string)).AllowDBNull = true;
        return table;
    }

    private void AddRow(DataTable table, LogEvent evt)
    {
        var row = table.NewRow();
        row[_columns.TimeUtc] = evt.TimeUtc.UtcDateTime;
        row[_columns.Level] = evt.Level.Key;
        row[_columns.Category] = evt.Category.Value;
        row[_columns.Message] = evt.Message;
        row[_columns.Template] = evt.MessageTemplate;
        row[_columns.Exception] = (object?)GetExceptionText(evt) ?? DBNull.Value;
        row[_columns.Properties] = (object?)GetPropertiesJson(evt) ?? DBNull.Value;
        table.Rows.Add(row);
    }

    private void MapColumns(SqlBulkCopy bulk)
    {
        // Explicit column maps so SqlBulkCopy does not reorder by
        // position if the target table carries extra columns (e.g. Id
        // IDENTITY) between the ones we write.
        bulk.ColumnMappings.Add(_columns.TimeUtc, _columns.TimeUtc);
        bulk.ColumnMappings.Add(_columns.Level, _columns.Level);
        bulk.ColumnMappings.Add(_columns.Category, _columns.Category);
        bulk.ColumnMappings.Add(_columns.Message, _columns.Message);
        bulk.ColumnMappings.Add(_columns.Template, _columns.Template);
        bulk.ColumnMappings.Add(_columns.Exception, _columns.Exception);
        bulk.ColumnMappings.Add(_columns.Properties, _columns.Properties);
    }

    private void EnsureTableOnce()
    {
        if (!_autoCreateTable) return;
        if (System.Threading.Interlocked.Exchange(ref _tableEnsured, 1) == 1) return;

        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = new SqlCommand(BuildCreateTableSql(), connection);
        command.ExecuteNonQuery();
    }

    private string BuildCreateTableSql()
    {
        // Idempotent: the IF NOT EXISTS guards DDL so concurrent
        // startups don't race-create the table.
        return $@"
IF NOT EXISTS (SELECT 1 FROM sys.tables t
               INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
               WHERE s.name = '{_schemaName}' AND t.name = '{_tableName}')
BEGIN
    CREATE TABLE {FullyQualifiedTable()} (
        [{_columns.Id}] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [{_columns.TimeUtc}] DATETIME2(7) NOT NULL,
        [{_columns.Level}] VARCHAR(32) NOT NULL,
        [{_columns.Category}] VARCHAR(128) NOT NULL,
        [{_columns.Message}] NVARCHAR(MAX) NOT NULL,
        [{_columns.Template}] NVARCHAR(MAX) NOT NULL,
        [{_columns.Exception}] NVARCHAR(MAX) NULL,
        [{_columns.Properties}] NVARCHAR(MAX) NULL
    );
END;";
    }

    private string FullyQualifiedTable() => $"[{_schemaName}].[{_tableName}]";

    private static string? GetExceptionText(LogEvent evt)
    {
        if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value) && value is Exception ex)
            return ex.ToString();
        return null;
    }

    private static string? GetPropertiesJson(LogEvent evt)
    {
        if (evt.Properties is null || evt.Properties.Count == 0) return null;

        // Hand-written JSON via Utf8JsonWriter — reflection-based
        // JsonSerializer.Serialize<T> is not AOT-clean, and the shape
        // here is trivial (a flat object mapping string names to string
        // values), so the hand-written path is both faster and AOT-safe.
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var prop in evt.Properties)
            {
                // ResolvedValue honours lazy Func<object?> properties and
                // swallows factory exceptions so one bad property does
                // not fail the whole insert.
                writer.WriteString(prop.Name, prop.ResolvedValue?.ToString());
            }
            writer.WriteEndObject();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
