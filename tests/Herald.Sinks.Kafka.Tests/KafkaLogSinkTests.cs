// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.Kafka;
using Herald.Sinks.Kafka.Providers;
using Xunit;

namespace Herald.Sinks.Kafka.Tests;

public sealed class KafkaLogSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_bootstrap_servers()
    {
        Action act = () => new KafkaLogSink(bootstrapServers: null!, topic: "logs");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_topic()
    {
        Action act = () => new KafkaLogSink(bootstrapServers: "localhost:9092", topic: null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_producer()
    {
        Action act = () => new KafkaLogSink(producer: null!, topic: "logs");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Provider_sink_kind_is_kafka()
    {
        new KafkaLogSinkProvider().SinkKind.Should().Be("kafka");
        KafkaLogSinkProvider.KindKey.Should().Be("kafka");
    }

}
