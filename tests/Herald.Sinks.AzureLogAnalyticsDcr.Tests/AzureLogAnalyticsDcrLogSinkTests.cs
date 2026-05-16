// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.AzureLogAnalyticsDcr;
using Herald.Sinks.AzureLogAnalyticsDcr.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.AzureLogAnalyticsDcr.Tests;

public sealed class AzureLogAnalyticsDcrLogSinkTests
{
    private const string Endpoint = "https://dce-stub.eastus-1.ingest.monitor.azure.com";
    private const string Rule = "dcr-abcd1234";
    private const string Stream = "Custom-HeraldLog";

    [Fact]
    public void Constructor_throws_on_null_endpoint()
    {
        Action act = () => new AzureLogAnalyticsDcrLogSink(null!, Rule, Stream);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_rule_id()
    {
        Action act = () => new AzureLogAnalyticsDcrLogSink(Endpoint, null!, Stream);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_stream_name()
    {
        Action act = () => new AzureLogAnalyticsDcrLogSink(Endpoint, Rule, null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Provider_sink_kind_is_azure_log_analytics_dcr()
    {
        new AzureLogAnalyticsDcrLogSinkProvider().SinkKind.Should().Be("azure_log_analytics_dcr");
        AzureLogAnalyticsDcrLogSinkProvider.KindKey.Should().Be("azure_log_analytics_dcr");
    }

}
