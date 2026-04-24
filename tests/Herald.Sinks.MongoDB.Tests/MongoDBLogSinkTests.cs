// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.MongoDB;
using Herald.Sinks.MongoDB.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.MongoDB.Tests;

public sealed class MongoDBLogSinkTests
{
    private const string Conn = "mongodb://localhost:27017";

    [Fact]
    public void Constructor_accepts_minimum_args()
    {
        Action act = () => new MongoDBLogSink(Conn, "herald", "logs");
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_throws_on_null_connection_string()
    {
        Action act = () => new MongoDBLogSink(null!, "herald", "logs");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_connection_string()
    {
        Action act = () => new MongoDBLogSink("", "herald", "logs");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_database_name()
    {
        Action act = () => new MongoDBLogSink(Conn, "", "logs");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_collection_name()
    {
        Action act = () => new MongoDBLogSink(Conn, "herald", "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_collection()
    {
        Action act = () => new MongoDBLogSink(collection: null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Provider_sink_kind_is_mongodb()
    {
        new MongoDBLogSinkProvider().SinkKind.Should().Be("mongodb");
        MongoDBLogSinkProvider.KindKey.Should().Be("mongodb");
    }

    [Fact]
    public void Provider_is_community_edition()
    {
        new MongoDBLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
