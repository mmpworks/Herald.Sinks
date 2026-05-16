// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.AwsCloudWatch;
using Herald.Sinks.AwsCloudWatch.Providers;
using Xunit;

namespace Herald.Sinks.AwsCloudWatch.Tests;

public sealed class AwsCloudWatchLogSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_log_group()
    {
        Action act = () => new AwsCloudWatchLogSink(logGroupName: null!, logStreamName: "s");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_log_group()
    {
        Action act = () => new AwsCloudWatchLogSink(logGroupName: "", logStreamName: "s");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_log_stream()
    {
        Action act = () => new AwsCloudWatchLogSink(logGroupName: "g", logStreamName: null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Provider_sink_kind_is_aws_cloudwatch()
    {
        new AwsCloudWatchLogSinkProvider().SinkKind.Should().Be("aws_cloudwatch");
        AwsCloudWatchLogSinkProvider.KindKey.Should().Be("aws_cloudwatch");
    }

}
