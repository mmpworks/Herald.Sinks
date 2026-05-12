// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.ApplicationInsightsSdk;
using Herald.Sinks.ApplicationInsightsSdk.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.ApplicationInsightsSdk.Tests;

public sealed class ApplicationInsightsLogSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_connection() =>
        ((Action)(() => new ApplicationInsightsLogSink(connectionString: null!))).Should().Throw<ArgumentException>();

    [Fact]
    public void Constructor_throws_on_null_client() =>
        ((Action)(() => new ApplicationInsightsLogSink(client: null!))).Should().Throw<ArgumentNullException>();

    [Fact]
    public void Provider_sink_kind_is_application_insights_sdk()
    {
        new ApplicationInsightsLogSinkProvider().SinkKind.Should().Be("application_insights_sdk");
        ApplicationInsightsLogSinkProvider.KindKey.Should().Be("application_insights_sdk");
    }

    [Fact]
    public void Provider_is_community_edition() =>
        new ApplicationInsightsLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
}
