// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.Mqtt;
using Herald.Sinks.Mqtt.Providers;
using Xunit;

namespace Herald.Sinks.Mqtt.Tests;

public sealed class MqttLogSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_client() =>
        ((Action)(() => new MqttLogSink(client: null!))).Should().Throw<ArgumentNullException>();

    [Fact]
    public void Provider_sink_kind_is_mqtt()
    {
        new MqttLogSinkProvider().SinkKind.Should().Be("mqtt");
        MqttLogSinkProvider.KindKey.Should().Be("mqtt");
    }
}
