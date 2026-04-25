// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.AzureServiceBus;
using Herald.Sinks.AzureServiceBus.Providers;
using MMP.Herald;
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

    [Fact]
    public void Provider_is_community_edition() =>
        new AzureServiceBusLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
}
