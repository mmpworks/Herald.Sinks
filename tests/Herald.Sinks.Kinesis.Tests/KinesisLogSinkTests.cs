// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Amazon;
using FluentAssertions;
using Herald.Sinks.Kinesis;
using Herald.Sinks.Kinesis.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.Kinesis.Tests;

public sealed class KinesisLogSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_stream() =>
        ((Action)(() => new KinesisLogSink(streamName: null!, RegionEndpoint.USEast1))).Should().Throw<ArgumentException>();

    [Fact]
    public void Constructor_throws_on_null_region() =>
        ((Action)(() => new KinesisLogSink(streamName: "s", region: null!))).Should().Throw<ArgumentNullException>();

    [Fact]
    public void Constructor_throws_on_null_client() =>
        ((Action)(() => new KinesisLogSink(client: null!, streamName: "s"))).Should().Throw<ArgumentNullException>();

    [Fact]
    public void Provider_sink_kind_is_kinesis()
    {
        new KinesisLogSinkProvider().SinkKind.Should().Be("kinesis");
        KinesisLogSinkProvider.KindKey.Should().Be("kinesis");
    }

    [Fact]
    public void Provider_is_community_edition() =>
        new KinesisLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
}
