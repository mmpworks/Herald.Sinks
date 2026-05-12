// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.Redis;
using Herald.Sinks.Redis.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.Redis.Tests;

public sealed class RedisLogSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_connection_string()
    {
        Action act = () => new RedisLogSink(connectionString: null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_connection_string()
    {
        Action act = () => new RedisLogSink(connectionString: "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_channel()
    {
        Action act = () => new RedisLogSink("localhost:6379", channelName: "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_subscriber()
    {
        Action act = () => new RedisLogSink(subscriber: null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Provider_sink_kind_is_redis()
    {
        new RedisLogSinkProvider().SinkKind.Should().Be("redis");
        RedisLogSinkProvider.KindKey.Should().Be("redis");
    }

    [Fact]
    public void Provider_is_community_edition()
    {
        new RedisLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
