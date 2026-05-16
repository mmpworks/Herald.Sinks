// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.RabbitMQ;
using Herald.Sinks.RabbitMQ.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.RabbitMQ.Tests;

/// <summary>
/// Construction-contract tests — any test that actually opens an AMQP
/// connection requires a broker, so those live in the manual
/// verification script (docs/manual-verify/rabbitmq.md). Constructor
/// tests exercise argument validation only.
/// </summary>
public sealed class RabbitMQLogSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_amqp_uri()
    {
        Action act = () => new RabbitMQLogSink(null!, "herald.logs");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_amqp_uri()
    {
        Action act = () => new RabbitMQLogSink("", "herald.logs");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_exchange()
    {
        Action act = () => new RabbitMQLogSink("amqp://localhost", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Provider_sink_kind_is_rabbitmq()
    {
        new RabbitMQLogSinkProvider().SinkKind.Should().Be("rabbitmq");
        RabbitMQLogSinkProvider.KindKey.Should().Be("rabbitmq");
    }

    [Fact]
    public void Provider_is_community_edition()
    {
        new RabbitMQLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
