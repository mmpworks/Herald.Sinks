// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.Rollbar;
using Herald.Sinks.Rollbar.Providers;
using Xunit;

namespace Herald.Sinks.Rollbar.Tests;

public sealed class RollbarLogSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_token() =>
        ((Action)(() => new RollbarLogSink(accessToken: null!))).Should().Throw<ArgumentException>();

    [Fact]
    public void Constructor_throws_on_empty_environment() =>
        ((Action)(() => new RollbarLogSink("token", environment: ""))).Should().Throw<ArgumentException>();

    [Fact]
    public void Constructor_accepts_minimum_args() =>
        ((Action)(() => new RollbarLogSink(accessToken: "tok"))).Should().NotThrow();

    [Fact]
    public void Provider_sink_kind_is_rollbar()
    {
        new RollbarLogSinkProvider().SinkKind.Should().Be("rollbar");
        RollbarLogSinkProvider.KindKey.Should().Be("rollbar");
    }
}
