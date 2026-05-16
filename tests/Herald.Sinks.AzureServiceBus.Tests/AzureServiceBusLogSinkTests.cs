// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.AzureServiceBus;
using Herald.Sinks.AzureServiceBus.Providers;
using Xunit;

namespace Herald.Sinks.AzureServiceBus.Tests;

public sealed class AzureServiceBusLogSinkTests
{
    private const string Conn = "Endpoint=sb://stub.servicebus.windows.net/;SharedAccessKeyName=stub;SharedAccessKey=stub";

    [Fact]
    public void Constructor_throws_on_null_connection() =>
        ((Action)(() => new AzureServiceBusLogSink(connectionString: null!, queueOrTopic: "q"))).Should().Throw<ArgumentException>();

    [Fact]
    public void Constructor_throws_on_null_target() =>
        ((Action)(() => new AzureServiceBusLogSink(Conn, queueOrTopic: null!))).Should().Throw<ArgumentException>();

    [Fact]
    public void Constructor_throws_on_null_sender() =>
        ((Action)(() => new AzureServiceBusLogSink(sender: null!))).Should().Throw<ArgumentNullException>();

    [Fact]
    public void Provider_sink_kind_is_azure_service_bus()
    {
        new AzureServiceBusLogSinkProvider().SinkKind.Should().Be("azure_service_bus");
        AzureServiceBusLogSinkProvider.KindKey.Should().Be("azure_service_bus");
    }
}
