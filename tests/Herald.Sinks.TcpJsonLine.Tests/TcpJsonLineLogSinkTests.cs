// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.TcpJsonLine;
using MMP.Herald.Levels;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.TcpJsonLine.Tests;

public sealed class TcpJsonLineLogSinkTests
{
    private readonly ILogLevelRegistry _registry = RegistryHelper.CreateDefault();

    [Fact]
    public void Null_host_throws() {
        var act = () => new TcpJsonLineLogSink(null!, 8080, _registry);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Empty_host_throws() {
        var act = () => new TcpJsonLineLogSink("", 8080, _registry);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Whitespace_host_throws() {
        var act = () => new TcpJsonLineLogSink("   ", 8080, _registry);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Zero_port_throws() {
        var act = () => new TcpJsonLineLogSink("localhost", 0, _registry);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Negative_port_throws() {
        var act = () => new TcpJsonLineLogSink("localhost", -1, _registry);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Port_above_65535_throws() {
        var act = () => new TcpJsonLineLogSink("localhost", 65536, _registry);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Null_registry_throws() {
        var act = () => new TcpJsonLineLogSink("localhost", 8080, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Valid_parameters_do_not_throw() {
        var act = () => new TcpJsonLineLogSink("localhost", 8080, _registry);
        act.Should().NotThrow();
    }

    [Fact]
    public void IsConnected_false_before_any_call() {
        using var sink = new TcpJsonLineLogSink("localhost", 8080, _registry);
        sink.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void Dispose_does_not_throw() {
        var sink = new TcpJsonLineLogSink("localhost", 8080, _registry);
        var act = () => sink.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Double_dispose_does_not_throw() {
        var sink = new TcpJsonLineLogSink("localhost", 8080, _registry);
        sink.Dispose();
        var act = () => sink.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Log_after_dispose_throws_ObjectDisposedException() {
        var sink = new TcpJsonLineLogSink("localhost", 8080, _registry);
        sink.Dispose();

        var evt = LogEventBuilder.Create().Build();
        var act = () => sink.Log(evt);
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Empty_batch_after_dispose_does_not_throw() {
        var sink = new TcpJsonLineLogSink("localhost", 8080, _registry);
        sink.Dispose();

        // Empty batch returns early before checking disposed state
        var act = () => sink.LogBatch([]);
        act.Should().NotThrow();
    }
}
