// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.AzureTableStorage;
using Herald.Sinks.AzureTableStorage.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.AzureTableStorage.Tests;

public sealed class AzureTableStorageLogSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_connection_string()
    {
        Action act = () => new AzureTableStorageLogSink(connectionString: null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_table_name()
    {
        Action act = () => new AzureTableStorageLogSink("UseDevelopmentStorage=true", tableName: "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Default_credential_overload_throws_when_flag_is_false()
    {
        Action act = () => new AzureTableStorageLogSink(
            "https://acct.table.core.windows.net", "HeraldLogs", useDefaultCredential: false);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_table_client()
    {
        Action act = () => new AzureTableStorageLogSink(tableClient: null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Provider_sink_kind_is_azure_table_storage()
    {
        new AzureTableStorageLogSinkProvider().SinkKind.Should().Be("azure_table_storage");
        AzureTableStorageLogSinkProvider.KindKey.Should().Be("azure_table_storage");
    }

    [Fact]
    public void Provider_is_community_edition()
    {
        new AzureTableStorageLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
