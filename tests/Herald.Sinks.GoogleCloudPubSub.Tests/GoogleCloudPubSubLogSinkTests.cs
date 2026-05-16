// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.GoogleCloudPubSub;
using Herald.Sinks.GoogleCloudPubSub.Providers;
using Xunit;

namespace Herald.Sinks.GoogleCloudPubSub.Tests;

public sealed class GoogleCloudPubSubLogSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_publisher() =>
        ((Action)(() => new GoogleCloudPubSubLogSink(publisher: null!))).Should().Throw<ArgumentNullException>();

    [Fact]
    public void Provider_sink_kind_is_google_pubsub()
    {
        new GoogleCloudPubSubLogSinkProvider().SinkKind.Should().Be("google_pubsub");
        GoogleCloudPubSubLogSinkProvider.KindKey.Should().Be("google_pubsub");
    }
}
