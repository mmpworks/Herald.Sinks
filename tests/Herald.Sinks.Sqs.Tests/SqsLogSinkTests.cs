// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Amazon;
using FluentAssertions;
using Herald.Sinks.Sqs;
using Herald.Sinks.Sqs.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.Sqs.Tests;

public sealed class SqsLogSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_queue() =>
        ((Action)(() => new SqsLogSink(queueUrl: null!, RegionEndpoint.USEast1))).Should().Throw<ArgumentException>();

    [Fact]
    public void Constructor_throws_on_null_region() =>
        ((Action)(() => new SqsLogSink(queueUrl: "https://q", region: null!))).Should().Throw<ArgumentNullException>();

    [Fact]
    public void Constructor_throws_on_null_client() =>
        ((Action)(() => new SqsLogSink(client: null!, queueUrl: "https://q"))).Should().Throw<ArgumentNullException>();

    [Fact]
    public void Provider_sink_kind_is_sqs()
    {
        new SqsLogSinkProvider().SinkKind.Should().Be("sqs");
        SqsLogSinkProvider.KindKey.Should().Be("sqs");
    }

    [Fact]
    public void Provider_is_community_edition() =>
        new SqsLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
}
