// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.Nats;
using Herald.Sinks.Nats.Providers;
using Xunit;

namespace Herald.Sinks.Nats.Tests;

public sealed class NatsLogSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_url() =>
        ((Action)(() => new NatsLogSink(url: null!))).Should().Throw<ArgumentException>();

    [Fact]
    public void Constructor_throws_on_empty_subject() =>
        ((Action)(() => new NatsLogSink("nats://localhost:4222", subject: ""))).Should().Throw<ArgumentException>();

    [Fact]
    public void Provider_sink_kind_is_nats()
    {
        new NatsLogSinkProvider().SinkKind.Should().Be("nats");
        NatsLogSinkProvider.KindKey.Should().Be("nats");
    }
}
