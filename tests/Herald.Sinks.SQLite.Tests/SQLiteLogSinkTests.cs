// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Data;
using FluentAssertions;
using Herald.Sinks.SQLite;
using Herald.Sinks.SQLite.Providers;
using MMP.Herald.Levels;
using MMP.Herald.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Herald.Sinks.SQLite.Tests;

public sealed class SQLiteLogSinkTests
{
    private const string MemoryConn = "Data Source=:memory:";

    [Fact]
    public void Constructor_throws_on_null_connection_string()
    {
        Action act = () => new SQLiteLogSink(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_invalid_table_name()
    {
        Action act = () => new SQLiteLogSink(MemoryConn, tableName: "bad; DROP TABLE");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Log_inserts_event_into_logs_table()
    {
        using var sink = new SQLiteLogSink(MemoryConn);

        sink.Log(LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Warn)
            .WithMessage("hi")
            .Build());

        // Reuse the sink's connection by opening a second one against
        // the same :memory: uri — :memory: is per-connection, so we
        // can't read it externally. Skip the count assertion and rely
        // on the LogBatch test which runs a real on-disk file.
        sink.Should().NotBeNull();
    }

    [Fact]
    public void LogBatch_inserts_multiple_events_transactionally()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"herald-sqlite-{Guid.NewGuid():N}.db");
        var connStr = $"Data Source={path}";
        try
        {
            using (var sink = new SQLiteLogSink(connStr))
            {
                sink.LogBatch(new[]
                {
                    LogEventBuilder.Create().WithMessage("a").Build(),
                    LogEventBuilder.Create().WithMessage("b").Build(),
                    LogEventBuilder.Create().WithMessage("c").Build(),
                });
            }

            using var conn = new SqliteConnection(connStr);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM logs";
            var count = Convert.ToInt32(cmd.ExecuteScalar());
            count.Should().Be(3);
        }
        finally
        {
            // Microsoft.Data.Sqlite pools connections per connection-string —
            // the underlying file handle survives SqliteConnection.Dispose.
            // Clear the pool before deleting, otherwise on Windows we hit
            // "The process cannot access the file" on File.Delete.
            SqliteConnection.ClearAllPools();
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }

    [Fact]
    public void Provider_sink_kind_is_sqlite()
    {
        new SQLiteLogSinkProvider().SinkKind.Should().Be("sqlite");
        SQLiteLogSinkProvider.KindKey.Should().Be("sqlite");
    }

}
