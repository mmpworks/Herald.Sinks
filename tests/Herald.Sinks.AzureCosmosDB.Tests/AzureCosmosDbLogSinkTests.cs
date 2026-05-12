// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.AzureCosmosDB;
using Herald.Sinks.AzureCosmosDB.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.AzureCosmosDB.Tests;

public sealed class AzureCosmosDbLogSinkTests
{
    // Cosmos' local emulator key; valid but harmless.
    private const string EmulatorEndpoint = "https://localhost:8081";
    private const string EmulatorKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    [Fact]
    public void Constructor_throws_on_null_endpoint()
    {
        Action act = () => new AzureCosmosDbLogSink(null!, EmulatorKey, "db", "c");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_auth_key()
    {
        Action act = () => new AzureCosmosDbLogSink(EmulatorEndpoint, null!, "db", "c");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_database_name()
    {
        Action act = () => new AzureCosmosDbLogSink(EmulatorEndpoint, EmulatorKey, null!, "c");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_container_name()
    {
        Action act = () => new AzureCosmosDbLogSink(EmulatorEndpoint, EmulatorKey, "db", null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Provider_sink_kind_is_azure_cosmosdb()
    {
        new AzureCosmosDbLogSinkProvider().SinkKind.Should().Be("azure_cosmosdb");
        AzureCosmosDbLogSinkProvider.KindKey.Should().Be("azure_cosmosdb");
    }

    [Fact]
    public void Provider_is_community_edition()
    {
        new AzureCosmosDbLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
