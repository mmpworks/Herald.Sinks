// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using MMP.Herald;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;
using MySqlConnector;

namespace Herald.Sinks.MySQL;

/// <summary>
/// Sink that writes log events as rows in a MySQL or MariaDB table
/// via <c>MySqlConnector</c>. Drop-in for Serilog.Sinks.MySQL.
/// Parameterized INSERT on single writes, transaction-wrapped batches
/// for IBatchedLogSink.
/// </summary>
/// <remarks>
/// <para>
/// <b>Schema.</b> The sink writes columns <c>time_utc</c> (DATETIME(6)),
/// <c>level</c>, <c>category</c>, <c>message</c>, <c>template</c>,
/// <c>exception</c>, <c>properties</c>. Set <c>autoCreateTable</c> to
/// true for dev; prefer installer-managed schema in production.
/// </para>
/// <para>
/// <b>MariaDB.</b> MariaDB 10.x accepts the same SQL; no sink changes
/// needed. Use MariaDB's native JSON type by declaring
/// <c>properties JSON</c> on the table.
/// </para>
/// </remarks>
public sealed class MySQLLogSink : ILogger, IBatchedLogSink
{
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly bool _autoCreateTable;
    private int _tableEnsured;

    public MySQLLogSink(string connectionString, string tableName = "logs", bool autoCreateTable = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        if (!IsValidIdentifier(tableName))
        {
            throw new ArgumentException(
                "Table name must contain only letters, digits, and underscores.",
                nameof(tableName));
        }

        _connectionString = connectionString;
        _tableName = tableName;
        _autoCreateTable = autoCreateTable;
    }

    public void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        EnsureTableOnce();

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        using var command = BuildInsertCommand(connection, logEvent);
        command.ExecuteNonQuery();
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;
        EnsureTableOnce();

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();
        using var tx = connection.BeginTransaction();

        foreach (var evt in events)
        {
            using var command = BuildInsertCommand(connection, evt);
            command.Transaction = tx;
            command.ExecuteNonQuery();
        }

        tx.Commit();
    }

    private MySqlCommand BuildInsertCommand(MySqlConnection connection, LogEvent evt)
    {
        var sql =
            $"INSERT INTO `{_tableName}` (time_utc, level, category, message, template, exception, properties) " +
            "VALUES (@time, @level, @category, @message, @template, @exception, @properties);";

        var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@time", evt.TimeUtc.UtcDateTime);
        command.Parameters.AddWithValue("@level", evt.Level.Key);
        command.Parameters.AddWithValue("@category", evt.Category.Value);
        command.Parameters.AddWithValue("@message", evt.Message ?? string.Empty);
        command.Parameters.AddWithValue("@template", evt.MessageTemplate ?? string.Empty);
        command.Parameters.AddWithValue("@exception", (object?)GetExceptionText(evt) ?? DBNull.Value);
        command.Parameters.AddWithValue("@properties", (object?)GetPropertiesJson(evt) ?? DBNull.Value);
        return command;
    }

    private void EnsureTableOnce()
    {
        if (!_autoCreateTable) return;
        if (System.Threading.Interlocked.Exchange(ref _tableEnsured, 1) == 1) return;

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();
        using var command = new MySqlCommand(BuildCreateTableSql(), connection);
        command.ExecuteNonQuery();
    }

    private string BuildCreateTableSql()
    {
        // CREATE TABLE IF NOT EXISTS is idempotent on MySQL 5.7+ /
        // MariaDB 10.x. The `properties` column uses TEXT for broadest
        // compat; switch to JSON in the installer for MySQL 5.7+ if
        // native JSON query support is needed.
        return
            $"CREATE TABLE IF NOT EXISTS `{_tableName}` (" +
            "  id BIGINT AUTO_INCREMENT PRIMARY KEY," +
            "  time_utc DATETIME(6) NOT NULL," +
            "  level VARCHAR(32) NOT NULL," +
            "  category VARCHAR(128) NOT NULL," +
            "  message TEXT NOT NULL," +
            "  template TEXT NOT NULL," +
            "  exception TEXT NULL," +
            "  properties TEXT NULL" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
    }

    private static string? GetExceptionText(LogEvent evt)
    {
        if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value) && value is Exception ex)
            return ex.ToString();
        return null;
    }

    private static string? GetPropertiesJson(LogEvent evt)
    {
        if (evt.Properties is null || evt.Properties.Count == 0) return null;

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

    private static bool IsValidIdentifier(string name)
    {
        foreach (var c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '_') return false;
        }
        return name.Length > 0;
    }
}
