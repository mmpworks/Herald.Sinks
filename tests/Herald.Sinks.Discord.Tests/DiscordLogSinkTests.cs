// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.Discord;
using Herald.Sinks.Discord.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.Discord.Tests;

public sealed class DiscordLogSinkTests
{
    [Fact] public void Constructor_throws_on_null_url() =>
        ((Action)(() => new DiscordLogSink(webhookUrl: null!))).Should().Throw<ArgumentException>();

    [Fact] public void Constructor_accepts_minimum_args() =>
        ((Action)(() => new DiscordLogSink("https://discord.com/api/webhooks/x/y"))).Should().NotThrow();

    [Fact] public void Provider_kind_and_edition()
    {
        new DiscordLogSinkProvider().SinkKind.Should().Be("discord");
        new DiscordLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
