// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.BetterStack;
using Herald.Sinks.BetterStack.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.BetterStack.Tests;

public sealed class BetterStackLogSinkTests
{
    [Fact] public void Constructor_throws_on_null_token() =>
        ((Action)(() => new BetterStackLogSink(sourceToken: null!))).Should().Throw<ArgumentException>();

    [Fact] public void Constructor_accepts_minimum_args() =>
        ((Action)(() => new BetterStackLogSink(sourceToken: "tok"))).Should().NotThrow();

    [Fact] public void Provider_kind_and_edition()
    {
        new BetterStackLogSinkProvider().SinkKind.Should().Be("betterstack");
        new BetterStackLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
