// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.Bugsnag;
using Herald.Sinks.Bugsnag.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.Bugsnag.Tests;

public sealed class BugsnagLogSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_api_key() =>
        ((Action)(() => new BugsnagLogSink(apiKey: null!))).Should().Throw<ArgumentException>();

    [Fact]
    public void Constructor_accepts_minimum_args() =>
        ((Action)(() => new BugsnagLogSink(apiKey: "abc"))).Should().NotThrow();

    [Fact]
    public void Provider_sink_kind_is_bugsnag()
    {
        new BugsnagLogSinkProvider().SinkKind.Should().Be("bugsnag");
        BugsnagLogSinkProvider.KindKey.Should().Be("bugsnag");
    }

    [Fact]
    public void Provider_is_community_edition() =>
        new BugsnagLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
}
