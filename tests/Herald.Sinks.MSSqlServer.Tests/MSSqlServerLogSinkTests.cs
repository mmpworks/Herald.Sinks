// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.MSSqlServer;
using Xunit;

namespace Herald.Sinks.MSSqlServer.Tests;

/// <summary>
/// Construction-contract tests. The INSERT / SqlBulkCopy paths require
/// a live SQL Server — those live in a separate manual-verification
/// script (see docs/manual-verify/mssql.md).
/// </summary>
public sealed class MSSqlServerLogSinkTests
{
    private const string Conn =
        "Server=localhost;Database=test;Trusted_Connection=True;TrustServerCertificate=True;";

    [Fact]
    public void Constructor_accepts_minimum_args()
    {
        Action act = () => new MSSqlServerLogSink(Conn);
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_throws_on_null_connection_string()
    {
        Action act = () => new MSSqlServerLogSink(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_connection_string()
    {
        Action act = () => new MSSqlServerLogSink("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_table_name()
    {
        Action act = () => new MSSqlServerLogSink(Conn, tableName: "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_schema_name()
    {
        Action act = () => new MSSqlServerLogSink(Conn, schemaName: "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Column_options_default_produces_expected_names()
    {
        var opts = MSSqlServerColumnOptions.Default;
        opts.Id.Should().Be("Id");
        opts.TimeUtc.Should().Be("TimeUtc");
        opts.Level.Should().Be("Level");
        opts.Category.Should().Be("Category");
        opts.Message.Should().Be("Message");
        opts.Template.Should().Be("Template");
        opts.Exception.Should().Be("Exception");
        opts.Properties.Should().Be("Properties");
    }

    [Fact]
    public void Column_options_accept_overrides()
    {
        var opts = new MSSqlServerColumnOptions
        {
            TimeUtc = "Timestamp",
            Message = "Msg",
        };
        opts.TimeUtc.Should().Be("Timestamp");
        opts.Message.Should().Be("Msg");
        opts.Level.Should().Be("Level");  // untouched
    }
}
