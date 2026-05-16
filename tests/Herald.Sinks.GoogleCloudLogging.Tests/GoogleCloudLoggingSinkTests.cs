// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.GoogleCloudLogging;
using Herald.Sinks.GoogleCloudLogging.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.GoogleCloudLogging.Tests;

public sealed class GoogleCloudLoggingSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_project_id()
    {
        Action act = () => new GoogleCloudLoggingSink(projectId: null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_project_id()
    {
        Action act = () => new GoogleCloudLoggingSink(projectId: "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_log_id()
    {
        Action act = () => new GoogleCloudLoggingSink(projectId: "my-project", logId: "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Provider_sink_kind_is_gcp_logging()
    {
        new GoogleCloudLoggingSinkProvider().SinkKind.Should().Be("gcp_logging");
        GoogleCloudLoggingSinkProvider.KindKey.Should().Be("gcp_logging");
    }

    [Fact]
    public void Provider_is_community_edition()
    {
        new GoogleCloudLoggingSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
