// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.AzureBlobStorage;
using Herald.Sinks.AzureBlobStorage.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.AzureBlobStorage.Tests;

public sealed class AzureBlobStorageLogSinkTests
{
    private const string Conn =
        "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

    [Fact]
    public void Constructor_throws_on_null_connection_string()
    {
        Action act = () => new AzureBlobStorageLogSink(connectionString: null!, containerName: "logs");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_container_name()
    {
        Action act = () => new AzureBlobStorageLogSink(connectionString: Conn, containerName: null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_key_prefix()
    {
        Action act = () => new AzureBlobStorageLogSink(connectionString: Conn, containerName: "logs", keyPrefix: null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Provider_sink_kind_is_azure_blob()
    {
        new AzureBlobStorageLogSinkProvider().SinkKind.Should().Be("azure_blob");
        AzureBlobStorageLogSinkProvider.KindKey.Should().Be("azure_blob");
    }
}
