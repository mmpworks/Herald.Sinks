// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.AmazonS3;
using Herald.Sinks.AmazonS3.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.AmazonS3.Tests;

public sealed class AmazonS3LogSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_bucket()
    {
        Action act = () => new AmazonS3LogSink(bucketName: null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_bucket()
    {
        Action act = () => new AmazonS3LogSink(bucketName: "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_key_prefix()
    {
        Action act = () => new AmazonS3LogSink(bucketName: "b", keyPrefix: null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Provider_sink_kind_is_aws_s3()
    {
        new AmazonS3LogSinkProvider().SinkKind.Should().Be("aws_s3");
        AmazonS3LogSinkProvider.KindKey.Should().Be("aws_s3");
    }

    [Fact]
    public void Provider_is_community_edition()
    {
        new AmazonS3LogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
