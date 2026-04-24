// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.PostgreSQL;
using Herald.Sinks.PostgreSQL.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.PostgreSQL.Tests;

/// <summary>
/// Construction-contract tests. The INSERT / COPY paths require a live
/// PostgreSQL — those live in a separate manual-verification script
/// (see docs/manual-verify/postgresql.md).
/// </summary>
public sealed class PostgreSQLLogSinkTests
{
    private const string Conn = "Host=localhost;Username=test;Password=test;Database=test;";

    [Fact]
    public void Constructor_accepts_minimum_args()
    {
        Action act = () => new PostgreSQLLogSink(Conn);
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_throws_on_null_connection_string()
    {
        Action act = () => new PostgreSQLLogSink(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_connection_string()
    {
        Action act = () => new PostgreSQLLogSink("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_table_name()
    {
        Action act = () => new PostgreSQLLogSink(Conn, tableName: "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_schema_name()
    {
        Action act = () => new PostgreSQLLogSink(Conn, schemaName: "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Column_options_default_uses_snake_case()
    {
        var opts = PostgreSQLColumnOptions.Default;
        opts.Id.Should().Be("id");
        opts.TimeUtc.Should().Be("time_utc");
        opts.Level.Should().Be("level");
        opts.Category.Should().Be("category");
        opts.Message.Should().Be("message");
        opts.Template.Should().Be("template");
        opts.Exception.Should().Be("exception");
        opts.Properties.Should().Be("properties");
    }

    [Fact]
    public void Column_options_accept_overrides()
    {
        var opts = new PostgreSQLColumnOptions
        {
            TimeUtc = "timestamp",
            Message = "msg",
        };
        opts.TimeUtc.Should().Be("timestamp");
        opts.Message.Should().Be("msg");
        opts.Level.Should().Be("level");
    }

    [Fact]
    public void Provider_sink_kind_is_postgresql()
    {
        new PostgreSQLLogSinkProvider().SinkKind.Should().Be("postgresql");
        PostgreSQLLogSinkProvider.KindKey.Should().Be("postgresql");
    }

    [Fact]
    public void Provider_is_community_edition()
    {
        new PostgreSQLLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
