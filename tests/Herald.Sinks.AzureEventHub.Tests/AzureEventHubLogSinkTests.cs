// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.AzureEventHub;
using Herald.Sinks.AzureEventHub.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.AzureEventHub.Tests;

public sealed class AzureEventHubLogSinkTests
{
    private const string Conn =
        "Endpoint=sb://stub.servicebus.windows.net/;SharedAccessKeyName=stub;SharedAccessKey=stub";

    [Fact]
    public void Constructor_throws_on_null_connection_string()
    {
        Action act = () => new AzureEventHubLogSink(connectionString: null!, eventHubName: "logs");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_event_hub_name()
    {
        Action act = () => new AzureEventHubLogSink(connectionString: Conn, eventHubName: null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Default_credential_overload_throws_when_flag_is_false()
    {
        Action act = () => new AzureEventHubLogSink("ns.servicebus.windows.net", "logs", useDefaultCredential: false);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Provider_sink_kind_is_azure_event_hub()
    {
        new AzureEventHubLogSinkProvider().SinkKind.Should().Be("azure_event_hub");
        AzureEventHubLogSinkProvider.KindKey.Should().Be("azure_event_hub");
    }

    [Fact]
    public void Provider_is_community_edition()
    {
        new AzureEventHubLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
