// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Net.Http;
using FluentAssertions;
using Herald.Sinks.InfluxDB;
using Herald.Sinks.InfluxDB.Providers;
using MMP.Herald;
using MMP.Herald.Levels;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.InfluxDB.Tests;

public sealed class InfluxDBLogSinkTests
{
    private const string Url = "http://localhost:8086";

    [Fact] public void Constructor_throws_on_null_url() =>
        ((Action)(() => new InfluxDBLogSink(serverUrl: null!, organization: "o", bucket: "b", token: "t"))).Should().Throw<ArgumentException>();

    [Fact] public void Constructor_throws_on_null_token() =>
        ((Action)(() => new InfluxDBLogSink("http://localhost:8086", "o", "b", token: null!))).Should().Throw<ArgumentException>();

    [Fact] public void Provider_throws_on_null_definition() =>
        ((Action)(() => new InfluxDBLogSinkProvider().CreateSink(definition: null!, levelRegistry: null!, transformerRegistry: null!))).Should().Throw<ArgumentNullException>();

    [Fact] public void Provider_kind_and_edition()
    {
        new InfluxDBLogSinkProvider().SinkKind.Should().Be("influxdb");
        new InfluxDBLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }

    // ── ADR-SINK-002: property preservation (default off) ────────────

    private static string CaptureLine(bool preserveProperties, int fieldLimit = InfluxDBLogSink.DefaultPreserveFieldLimit)
    {
        var handler = new TestHttpMessageHandler();
        using var client = new HttpClient(handler);
        using var sink = new InfluxDBLogSink(Url, "org", "bucket", "token",
            httpClient: client,
            preserveProperties: preserveProperties,
            preserveFieldLimit: fieldLimit);

        var evt = LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Info)
            .WithMessage("hello", "hello")
            .WithProperty("UserId", 42L)
            .WithProperty("IP", "10.0.0.1")
            .WithProperty("Active", true)
            .WithProperty("Ratio", 1.5)
            .Build();

        sink.Log(evt);
        return handler.LastRequestBodyString!.TrimEnd('\n');
    }

    [Fact]
    public void Preservation_off_drops_properties_byte_for_byte()
    {
        var line = CaptureLine(preserveProperties: false);

        // Today's behaviour: only message rides as a field; no property fields.
        line.Should().Contain("message=\"hello\"");
        line.Should().NotContain("UserId");
        line.Should().NotContain("IP=");
        line.Should().NotContain("Active");
        line.Should().NotContain("Ratio");
    }

    [Fact]
    public void Preservation_on_carries_properties_as_type_mapped_fields()
    {
        var line = CaptureLine(preserveProperties: true);

        // Fields are in the field section (after the first space), not the
        // tag section. level/category stay tags; properties are fields.
        var firstSpace = line.IndexOf(' ');
        var tagSection = line.Substring(0, firstSpace);
        var fieldSection = line.Substring(firstSpace + 1);

        tagSection.Should().Contain("level=info");
        tagSection.Should().Contain("category=");
        tagSection.Should().NotContain("UserId");
        tagSection.Should().NotContain("IP");

        // Type-mapped fields: long -> 42i, string -> "...", bool -> true,
        // double -> 1.5.
        fieldSection.Should().Contain("UserId=42i");
        fieldSection.Should().Contain("IP=\"10.0.0.1\"");
        fieldSection.Should().Contain("Active=true");
        fieldSection.Should().Contain("Ratio=1.5");
    }

    [Fact]
    public void Preservation_on_respects_soft_cap()
    {
        // Cap of 1 preserved field: only the first property survives; the
        // rest are skipped (the skip is the safety).
        var line = CaptureLine(preserveProperties: true, fieldLimit: 1);

        // Exactly one preserved field beyond message. UserId is first.
        line.Should().Contain("UserId=42i");
        line.Should().NotContain("Active=");
        line.Should().NotContain("Ratio=");
    }
}
