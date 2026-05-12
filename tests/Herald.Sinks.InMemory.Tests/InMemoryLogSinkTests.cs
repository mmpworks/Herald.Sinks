// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Herald.Sinks.InMemory;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.InMemory.Tests;

public sealed class InMemoryLogSinkTests
{
    [Fact]
    public void Log_retains_events_in_order()
    {
        var sink = new InMemoryLogSink();

        sink.Log(LogEventBuilder.Create().WithMessage("first").Build());
        sink.Log(LogEventBuilder.Create().WithMessage("second").Build());
        sink.Log(LogEventBuilder.Create().WithMessage("third").Build());

        sink.Events.Should().HaveCount(3);
        sink.Events[0].Message.Should().Be("first");
        sink.Events[1].Message.Should().Be("second");
        sink.Events[2].Message.Should().Be("third");
    }

    [Fact]
    public void Count_returns_retained_event_count()
    {
        var sink = new InMemoryLogSink();

        sink.Count.Should().Be(0);
        sink.Log(LogEventBuilder.Create().Build());
        sink.Count.Should().Be(1);
        sink.Log(LogEventBuilder.Create().Build());
        sink.Count.Should().Be(2);
    }

    [Fact]
    public void Clear_discards_all_events()
    {
        var sink = new InMemoryLogSink();
        sink.Log(LogEventBuilder.Create().Build());
        sink.Log(LogEventBuilder.Create().Build());

        sink.Clear();

        sink.Events.Should().BeEmpty();
        sink.Count.Should().Be(0);
    }

    [Fact]
    public void Capacity_drops_oldest_event_when_full()
    {
        var sink = new InMemoryLogSink(capacity: 2);

        sink.Log(LogEventBuilder.Create().WithMessage("a").Build());
        sink.Log(LogEventBuilder.Create().WithMessage("b").Build());
        sink.Log(LogEventBuilder.Create().WithMessage("c").Build());

        sink.Count.Should().Be(2);
        sink.Events[0].Message.Should().Be("b");
        sink.Events[1].Message.Should().Be("c");
    }

    [Fact]
    public void Events_snapshot_is_independent_of_further_logging()
    {
        var sink = new InMemoryLogSink();
        sink.Log(LogEventBuilder.Create().WithMessage("first").Build());

        var snapshot = sink.Events;
        sink.Log(LogEventBuilder.Create().WithMessage("second").Build());

        snapshot.Should().HaveCount(1);
        sink.Events.Should().HaveCount(2);
    }

    [Fact]
    public async Task Log_is_thread_safe_under_concurrent_writers()
    {
        var sink = new InMemoryLogSink();
        const int writerCount = 8;
        const int eventsPerWriter = 250;

        await Task.WhenAll(Enumerable.Range(0, writerCount).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < eventsPerWriter; i++)
                sink.Log(LogEventBuilder.Create().Build());
        })));

        sink.Count.Should().Be(writerCount * eventsPerWriter);
    }

    [Fact]
    public void Constructor_rejects_non_positive_capacity()
    {
        Action zero = () => new InMemoryLogSink(capacity: 0);
        Action neg = () => new InMemoryLogSink(capacity: -1);

        zero.Should().Throw<ArgumentOutOfRangeException>();
        neg.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Log_throws_on_null_event()
    {
        var sink = new InMemoryLogSink();
        Action act = () => sink.Log(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
