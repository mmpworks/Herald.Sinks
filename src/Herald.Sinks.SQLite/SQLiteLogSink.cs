// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using MMP.Herald;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;

namespace Herald.Sinks.SQLite;

/// <summary>
/// Sink that writes log events to an embedded SQLite database.
/// Drop-in for Serilog.Sinks.SQLite. Single-file, no server, no
/// background process. Ideal for desktop apps, CLI tools, edge
/// devices, and anywhere else a full DB install is overkill.
/// </summary>
/// <remarks>
/// <para>
/// <b>Schema.</b> Creates a <c>logs</c> table (overridable) with
/// columns <c>id</c> INTEGER PRIMARY KEY, <c>time_utc</c> TEXT,
/// <c>level</c> TEXT, <c>category</c> TEXT, <c>message</c> TEXT,
/// <c>template</c> TEXT, <c>exception</c> TEXT, <c>properties</c>
/// TEXT (JSON). The sink issues CREATE TABLE IF NOT EXISTS on
/// construction.
/// </para>
/// <para>
/// <b>Concurrency.</b> SQLite handles concurrent readers but
/// serializes writers. The sink uses a single connection held open
/// for its lifetime with a per-sink lock around writes. For a
/// multi-process writer pattern, open WAL mode via the connection
/// string (<c>Mode=ReadWrite;Cache=Shared</c> + a PRAGMA journal_mode
/// step).
/// </para>
/// </remarks>
public sealed class SQLiteLogSink : ILogger, IBatchedLogSink, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _tableName;
    private readonly object _writeLock = new();
    private int _disposed;

    public SQLiteLogSink(string connectionString, string tableName = "logs")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        if (!IsValidIdentifier(tableName))
        {
            throw new ArgumentException(
                "Table name must contain only letters, digits, and underscores.",
                nameof(tableName));
        }

        _tableName = tableName;
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        EnsureSchema();
    }

    public void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        if (System.Threading.Volatile.Read(ref _disposed) == 1) return;

        lock (_writeLock)
        {
            using var command = BuildInsertCommand(logEvent);
            command.ExecuteNonQuery();
        }
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;
        if (System.Threading.Volatile.Read(ref _disposed) == 1) return;

        lock (_writeLock)
        {
            // Wrap the batch in a transaction — SQLite commits every
            // write independently otherwise, which is ~10x slower for
            // bulk inserts.
            using var tx = _connection.BeginTransaction();
            foreach (var evt in events)
            {
                using var command = BuildInsertCommand(evt);
                command.Transaction = tx;
                command.ExecuteNonQuery();
            }
            tx.Commit();
        }
    }

    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) == 1) return;
        _connection.Dispose();
    }

    private SqliteCommand BuildInsertCommand(LogEvent evt)
    {
        var sql =
            $"INSERT INTO {_tableName} (time_utc, level, category, message, template, exception, properties) " +
            "VALUES (@time, @level, @category, @message, @template, @exception, @properties);";

        var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@time", evt.TimeUtc.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("@level", evt.Level.Key);
        command.Parameters.AddWithValue("@category", evt.Category.Value);
        command.Parameters.AddWithValue("@message", evt.Message ?? string.Empty);
        command.Parameters.AddWithValue("@template", evt.MessageTemplate ?? string.Empty);
        command.Parameters.AddWithValue("@exception", (object?)GetExceptionText(evt) ?? DBNull.Value);
        command.Parameters.AddWithValue("@properties", (object?)GetPropertiesJson(evt) ?? DBNull.Value);
        return command;
    }

    private void EnsureSchema()
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            $"CREATE TABLE IF NOT EXISTS {_tableName} (" +
            "  id         INTEGER PRIMARY KEY AUTOINCREMENT," +
            "  time_utc   TEXT NOT NULL," +
            "  level      TEXT NOT NULL," +
            "  category   TEXT NOT NULL," +
            "  message    TEXT NOT NULL," +
            "  template   TEXT NOT NULL," +
            "  exception  TEXT NULL," +
            "  properties TEXT NULL" +
            ");";
        command.ExecuteNonQuery();
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
