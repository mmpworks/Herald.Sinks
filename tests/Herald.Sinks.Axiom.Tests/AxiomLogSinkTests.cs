// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.Axiom;
using Herald.Sinks.Axiom.Providers;
using MMP.Herald;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Axiom.Tests;

public sealed class AxiomLogSinkTests
{
    [Fact] public void Constructor_throws_on_null_token() =>
        ((Action)(() => new AxiomLogSink(apiToken: null!, dataset: "d"))).Should().Throw<ArgumentException>();

    [Fact] public void Constructor_throws_on_null_dataset() =>
        ((Action)(() => new AxiomLogSink(apiToken: "t", dataset: null!))).Should().Throw<ArgumentException>();

    [Fact] public void Provider_kind_and_edition()
    {
        new AxiomLogSinkProvider().SinkKind.Should().Be("axiom");
        new AxiomLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }

    // Regression for the 2026-05-30 sink async audit (F-1, F-2). Axiom is the
    // batched HTTP representative — its sync LogBatch used to run
    // `_http.SendAsync(request).GetAwaiter().GetResult()` with no
    // ConfigureAwait(false). The fix routes the sync path through
    // `HttpClient.Send`. Proves Log returns under a single-threaded
    // SynchronizationContext instead of deadlocking.
    [Fact]
    public void Log_does_not_deadlock_under_single_thread_sync_context()
    {
        using var http = SyncContextDeadlockProbe.YieldingClient();
        using var sink = new AxiomLogSink("token", "dataset", httpClient: http);
        var evt = LogEventBuilder.Create().WithMessage("deadlock probe").Build();

        var completed = SyncContextDeadlockProbe.RunUnderSingleThreadContext(() => sink.Log(evt));

        completed.Should().BeTrue(
            "the sync LogBatch path must use HttpClient.Send and carry no captured-context dependency");
    }
}
