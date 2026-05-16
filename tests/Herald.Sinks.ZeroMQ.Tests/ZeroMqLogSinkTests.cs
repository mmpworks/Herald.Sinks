// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.ZeroMQ;
using Herald.Sinks.ZeroMQ.Providers;
using Xunit;

namespace Herald.Sinks.ZeroMQ.Tests;

public sealed class ZeroMqLogSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_endpoint()
    {
        Action act = () => new ZeroMqLogSink(endpoint: null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_endpoint()
    {
        Action act = () => new ZeroMqLogSink(endpoint: "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_topic()
    {
        Action act = () => new ZeroMqLogSink("inproc://herald-test", topic: "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Provider_sink_kind_is_zeromq()
    {
        new ZeroMqLogSinkProvider().SinkKind.Should().Be("zeromq");
        ZeroMqLogSinkProvider.KindKey.Should().Be("zeromq");
    }

    [Fact]
    public void SocketKind_enum_has_pubsub_and_pushpull()
    {
        Enum.IsDefined(typeof(ZeroMqSocketKind), ZeroMqSocketKind.PubSub).Should().BeTrue();
        Enum.IsDefined(typeof(ZeroMqSocketKind), ZeroMqSocketKind.PushPull).Should().BeTrue();
    }
}
