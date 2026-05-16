// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using Amazon;
using FluentAssertions;
using Herald.Sinks.DynamoDB;
using Herald.Sinks.DynamoDB.Providers;
using Xunit;

namespace Herald.Sinks.DynamoDB.Tests;

public sealed class DynamoDBLogSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_table_name()
    {
        Action act = () => new DynamoDBLogSink(tableName: null!, RegionEndpoint.USEast1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_table_name()
    {
        Action act = () => new DynamoDBLogSink(tableName: "", RegionEndpoint.USEast1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_region()
    {
        Action act = () => new DynamoDBLogSink(tableName: "logs", region: null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_throws_on_null_client()
    {
        Action act = () => new DynamoDBLogSink(client: null!, tableName: "logs");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Provider_sink_kind_is_dynamodb()
    {
        new DynamoDBLogSinkProvider().SinkKind.Should().Be("dynamodb");
        DynamoDBLogSinkProvider.KindKey.Should().Be("dynamodb");
    }

}
