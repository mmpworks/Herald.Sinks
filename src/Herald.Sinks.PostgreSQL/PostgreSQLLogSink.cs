// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;
using Npgsql;
using NpgsqlTypes;

namespace Herald.Sinks.PostgreSQL;

/// <summary>
/// Sink that writes log events as rows in a PostgreSQL table.
/// Parameterized INSERT for single-event <c>Log</c> calls, COPY binary
/// protocol via <see cref="NpgsqlBinaryImporter"/> for batches.
/// </summary>
/// <remarks>
/// <para>
/// <b>Schema.</b> See <see cref="PostgreSQLColumnOptions"/> remarks for
/// the column shape the sink expects. The sink does not write the
/// <c>id</c> column — PostgreSQL's BIGSERIAL supplies it.
/// </para>
/// <para>
/// <b>Auto-create.</b> When <c>autoCreateTable</c> is true the sink
/// issues <c>CREATE TABLE IF NOT EXISTS</c> DDL on first write. Prefer
/// installer-managed schema in production.
/// </para>
/// <para>
/// <b>Thread safety.</b> The sink is thread-safe. Each call opens a
/// fresh pooled connection; Npgsql's connection pool handles
/// concurrency.
/// </para>
/// </remarks>
public sealed class PostgreSQLLogSink : HeraldSinkBase, IBatchedLogSink
{
    private readonly string _connectionString;
    private readonly string _schemaName;
    private readonly string _tableName;
    private readonly PostgreSQLColumnOptions _columns;
    private readonly bool _autoCreateTable;
    private int _tableEnsured;

    public PostgreSQLLogSink(
        string connectionString,
        string tableName = "logs",
        string schemaName = "public",
        PostgreSQLColumnOptions? columns = null,
        bool autoCreateTable = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);

        _connectionString = connectionString;
        _tableName = tableName;
        _schemaName = schemaName;
        _columns = columns ?? PostgreSQLColumnOptions.Default;
        _autoCreateTable = autoCreateTable;
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        EnsureTableOnce();

        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = BuildInsertCommand(connection, logEvent);
        command.ExecuteNonQuery();
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;
        EnsureTableOnce();

        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        // Npgsql's binary COPY is the fast path for bulk insert — one
        // network round-trip for the whole batch, no per-row parsing
        // on the server.
        var copyCommand =
            $"COPY {FullyQualifiedTable()} " +
            $"(\"{_columns.TimeUtc}\", \"{_columns.Level}\", \"{_columns.Category}\", " +
            $"\"{_columns.Message}\", \"{_columns.Template}\", \"{_columns.Exception}\", " +
            $"\"{_columns.Properties}\") " +
            $"FROM STDIN (FORMAT BINARY)";

        using var writer = connection.BeginBinaryImport(copyCommand);

        foreach (var evt in events)
        {
            writer.StartRow();
            writer.Write(evt.TimeUtc.UtcDateTime, NpgsqlDbType.TimestampTz);
            writer.Write(evt.Level.Key, NpgsqlDbType.Text);
            writer.Write(evt.Category.Value, NpgsqlDbType.Text);
            writer.Write(evt.Message, NpgsqlDbType.Text);
            writer.Write(evt.MessageTemplate, NpgsqlDbType.Text);

            var exceptionText = GetExceptionText(evt);
            if (exceptionText is null) writer.WriteNull();
            else writer.Write(exceptionText, NpgsqlDbType.Text);

            var propertiesJson = GetPropertiesJson(evt);
            if (propertiesJson is null) writer.WriteNull();
            else writer.Write(propertiesJson, NpgsqlDbType.Jsonb);
        }

        writer.Complete();
    }

    private NpgsqlCommand BuildInsertCommand(NpgsqlConnection connection, LogEvent evt)
    {
        var sql =
            $"INSERT INTO {FullyQualifiedTable()} " +
            $"(\"{_columns.TimeUtc}\", \"{_columns.Level}\", \"{_columns.Category}\", " +
            $"\"{_columns.Message}\", \"{_columns.Template}\", \"{_columns.Exception}\", " +
            $"\"{_columns.Properties}\") " +
            $"VALUES (@time, @level, @category, @message, @template, @exception, @properties::jsonb);";

        var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("time", NpgsqlDbType.TimestampTz, evt.TimeUtc.UtcDateTime);
        command.Parameters.AddWithValue("level", NpgsqlDbType.Text, evt.Level.Key);
        command.Parameters.AddWithValue("category", NpgsqlDbType.Text, evt.Category.Value);
        command.Parameters.AddWithValue("message", NpgsqlDbType.Text, evt.Message);
        command.Parameters.AddWithValue("template", NpgsqlDbType.Text, evt.MessageTemplate);
        command.Parameters.AddWithValue("exception", NpgsqlDbType.Text, (object?)GetExceptionText(evt) ?? DBNull.Value);
        command.Parameters.AddWithValue("properties", NpgsqlDbType.Text, (object?)GetPropertiesJson(evt) ?? DBNull.Value);
        return command;
    }

    private void EnsureTableOnce()
    {
        if (!_autoCreateTable) return;
        if (System.Threading.Interlocked.Exchange(ref _tableEnsured, 1) == 1) return;

        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(BuildCreateTableSql(), connection);
        command.ExecuteNonQuery();
    }

    private string BuildCreateTableSql()
    {
        // IF NOT EXISTS is idempotent; concurrent startups cannot
        // race-create the same table.
        return $@"
CREATE TABLE IF NOT EXISTS {FullyQualifiedTable()} (
    ""{_columns.Id}"" BIGSERIAL PRIMARY KEY,
    ""{_columns.TimeUtc}"" TIMESTAMPTZ NOT NULL,
    ""{_columns.Level}"" TEXT NOT NULL,
    ""{_columns.Category}"" TEXT NOT NULL,
    ""{_columns.Message}"" TEXT NOT NULL,
    ""{_columns.Template}"" TEXT NOT NULL,
    ""{_columns.Exception}"" TEXT NULL,
    ""{_columns.Properties}"" JSONB NULL
);";
    }

    private string FullyQualifiedTable() => $"\"{_schemaName}\".\"{_tableName}\"";

    private static string? GetExceptionText(LogEvent evt)
    {
        if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value) && value is Exception ex)
            return ex.ToString();
        return null;
    }

    private static string? GetPropertiesJson(LogEvent evt)
    {
        if (evt.Properties is null || evt.Properties.Count == 0) return null;

        // Hand-written JSON via Utf8JsonWriter so the sink stays
        // AOT-clean. Flat object; each property name maps to a string
        // representation of ResolvedValue.
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var prop in evt.Properties)
            {
                writer.WriteString(prop.Name, prop.ResolvedValue?.ToString());
            }
            writer.WriteEndObject();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
