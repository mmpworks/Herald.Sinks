// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.InfluxDB;
using Herald.Sinks.InfluxDB.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.InfluxDB.Tests;

public sealed class InfluxDBLogSinkTests
{
    [Fact] public void Constructor_throws_on_null_url() =>
        ((Action)(() => new InfluxDBLogSink(serverUrl: null!, organization: "o", bucket: "b", token: "t"))).Should().Throw<ArgumentException>();

    [Fact] public void Constructor_throws_on_null_token() =>
        ((Action)(() => new InfluxDBLogSink("http://localhost:8086", "o", "b", token: null!))).Should().Throw<ArgumentException>();

    [Fact] public void Provider_throws_on_create_sink() =>
        ((Action)(() => new InfluxDBLogSinkProvider().CreateSink(definition: null!, levelRegistry: null!, transformerRegistry: null!))).Should().Throw<NotSupportedException>();

    [Fact] public void Provider_kind_and_edition()
    {
        new InfluxDBLogSinkProvider().SinkKind.Should().Be("influxdb");
        new InfluxDBLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
