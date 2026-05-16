// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.MySQL;
using Herald.Sinks.MySQL.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.MySQL.Tests;

public sealed class MySQLLogSinkTests
{
    private const string Conn = "Server=localhost;Database=test;User ID=test;Password=test;";

    [Fact]
    public void Constructor_accepts_minimum_args()
    {
        Action act = () => new MySQLLogSink(Conn);
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_throws_on_null_connection_string()
    {
        Action act = () => new MySQLLogSink(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_invalid_table_name()
    {
        Action act = () => new MySQLLogSink(Conn, tableName: "bad-name!");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Provider_sink_kind_is_mysql()
    {
        new MySQLLogSinkProvider().SinkKind.Should().Be("mysql");
        MySQLLogSinkProvider.KindKey.Should().Be("mysql");
    }

    [Fact]
    public void Provider_is_community_edition()
    {
        new MySQLLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
