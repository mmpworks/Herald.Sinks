// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.RedisList;
using Herald.Sinks.RedisList.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.RedisList.Tests;

public sealed class RedisListLogSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_connection_string()
    {
        Action act = () => new RedisListLogSink(connectionString: null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_list_key()
    {
        Action act = () => new RedisListLogSink("localhost:6379", listKey: "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_database()
    {
        Action act = () => new RedisListLogSink(database: null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Provider_sink_kind_is_redis_list()
    {
        new RedisListLogSinkProvider().SinkKind.Should().Be("redis_list");
        RedisListLogSinkProvider.KindKey.Should().Be("redis_list");
    }

    [Fact]
    public void Provider_is_community_edition()
    {
        new RedisListLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
