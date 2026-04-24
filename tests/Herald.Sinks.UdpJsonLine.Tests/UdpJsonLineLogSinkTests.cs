// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Herald.Sinks.UdpJsonLine;
using MMP.Herald.Levels;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.UdpJsonLine.Tests;

public sealed class UdpJsonLineLogSinkTests
{
    private readonly ILogLevelRegistry _registry = RegistryHelper.CreateDefault();

    [Fact]
    public void Null_host_throws() {
        var act = () => new UdpJsonLineLogSink(null!, 8080, _registry);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Empty_host_throws() {
        var act = () => new UdpJsonLineLogSink("", 8080, _registry);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Whitespace_host_throws() {
        var act = () => new UdpJsonLineLogSink("   ", 8080, _registry);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Out_of_range_port_throws(int port) {
        var act = () => new UdpJsonLineLogSink("localhost", port, _registry);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Null_registry_throws() {
        var act = () => new UdpJsonLineLogSink("localhost", 8080, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Valid_parameters_do_not_throw() {
        var act = () => new UdpJsonLineLogSink("localhost", 8080, _registry);
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_does_not_throw() {
        var sink = new UdpJsonLineLogSink("localhost", 8080, _registry);
        var act = sink.Dispose;
        act.Should().NotThrow();
    }

    [Fact]
    public void Double_dispose_does_not_throw() {
        var sink = new UdpJsonLineLogSink("localhost", 8080, _registry);
        sink.Dispose();
        var act = sink.Dispose;
        act.Should().NotThrow();
    }

    [Fact]
    public void Log_after_dispose_throws() {
        var sink = new UdpJsonLineLogSink("localhost", 8080, _registry);
        sink.Dispose();

        var evt = LogEventBuilder.Create().Build();
        var act = () => sink.Log(evt);
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task LogAsync_sends_datagram_that_a_local_receiver_picks_up() {
        // Bind a UdpClient to an OS-assigned port and have the sink send to it.
        using var receiver = new UdpClient(0, AddressFamily.InterNetwork);
        var port = ((IPEndPoint)receiver.Client.LocalEndPoint!).Port;

        using var sink = new UdpJsonLineLogSink(IPAddress.Loopback.ToString(), port, _registry);
        var evt = LogEventBuilder.Create().WithMessage("udp-test-payload").Build();

        await sink.LogAsync(evt);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = await receiver.ReceiveAsync(cts.Token);
        var text = Encoding.UTF8.GetString(received.Buffer);

        text.Should().Contain("udp-test-payload");
    }
}
