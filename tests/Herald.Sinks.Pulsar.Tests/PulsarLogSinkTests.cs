// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.Pulsar;
using Herald.Sinks.Pulsar.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.Pulsar.Tests;

public sealed class PulsarLogSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_url() =>
        ((Action)(() => new PulsarLogSink(serviceUrl: null!, topic: "t"))).Should().Throw<ArgumentException>();

    [Fact]
    public void Constructor_throws_on_null_topic() =>
        ((Action)(() => new PulsarLogSink("pulsar://localhost:6650", topic: null!))).Should().Throw<ArgumentException>();

    [Fact]
    public void Constructor_throws_on_null_producer() =>
        ((Action)(() => new PulsarLogSink(producer: null!))).Should().Throw<ArgumentNullException>();

    [Fact]
    public void Provider_sink_kind_is_pulsar()
    {
        new PulsarLogSinkProvider().SinkKind.Should().Be("pulsar");
        PulsarLogSinkProvider.KindKey.Should().Be("pulsar");
    }

    [Fact]
    public void Provider_is_community_edition() =>
        new PulsarLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
}
