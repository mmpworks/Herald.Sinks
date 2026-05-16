// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.ClickHouse;
using Herald.Sinks.ClickHouse.Providers;
using Xunit;

namespace Herald.Sinks.ClickHouse.Tests;

public sealed class ClickHouseLogSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_connection() =>
        ((Action)(() => new ClickHouseLogSink(connectionString: null!))).Should().Throw<ArgumentException>();

    [Fact]
    public void Constructor_throws_on_invalid_table_name() =>
        ((Action)(() => new ClickHouseLogSink("Host=localhost", tableName: "drop;table"))).Should().Throw<ArgumentException>();

    [Fact]
    public void Provider_sink_kind_is_clickhouse()
    {
        new ClickHouseLogSinkProvider().SinkKind.Should().Be("clickhouse");
        ClickHouseLogSinkProvider.KindKey.Should().Be("clickhouse");
    }
}
