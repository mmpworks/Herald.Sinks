// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.Discord;
using Herald.Sinks.Discord.Providers;
using MMP.Herald;
using MMP.Herald.Tests.Helpers;
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

    // Regression for the 2026-05-30 sink async audit (F-1, F-2). Discord is
    // the exemplar single-event HTTP sink. Before the fix its sync Log ran
    // `_http.PostAsync(...).GetAwaiter().GetResult()` with no
    // ConfigureAwait(false) — a hard deadlock on a SynchronizationContext
    // host (legacy ASP.NET / WPF / WinForms). The fix routes sync Log
    // through `HttpClient.Send` (true sync, no continuation). This test
    // installs a single-threaded context and proves Log returns instead of
    // deadlocking.
    [Fact]
    public void Log_does_not_deadlock_under_single_thread_sync_context()
    {
        using var http = SyncContextDeadlockProbe.YieldingClient();
        using var sink = new DiscordLogSink("https://discord.com/api/webhooks/x/y", http);
        var evt = LogEventBuilder.Create().WithMessage("deadlock probe").Build();

        var completed = SyncContextDeadlockProbe.RunUnderSingleThreadContext(() => sink.Log(evt));

        completed.Should().BeTrue(
            "the sync Log path must use HttpClient.Send and carry no captured-context dependency");
    }
}
