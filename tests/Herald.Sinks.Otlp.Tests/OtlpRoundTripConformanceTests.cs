// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using FluentAssertions;
using Herald.Sinks.Otlp;
using MMP.Herald.Addons.OtlpSinks;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Templating;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Otlp.Tests;

/// <summary>
/// Round-trip conformance pins for Jared's OTLP fidelity findings. Each sink
/// encodes a LogEvent; the OSS-side decoder reads it back. The bar is that a
/// no-modification event survives encode -> decode with its attribute types,
/// sub-millisecond timestamp, and empty-string values intact.
///
/// F1: non-string attributes survive as their AnyValue kind (int/double/bool).
/// F2: sub-millisecond timestamp precision survives to the 100ns floor.
/// F4: an empty-string attribute survives as an explicit empty string.
/// </summary>
public sealed class OtlpRoundTripConformanceTests
{
    private readonly ILogLevelRegistry _registry = RegistryHelper.CreateDefault();

    // A timestamp carrying sub-millisecond (100ns-tick) precision. 1234567 ticks
    // past the second is 123.4567 ms — the trailing 4567 ticks would be lost by a
    // millisecond-truncating encoder.
    private static readonly DateTimeOffset SubMsTime =
        new DateTimeOffset(2026, 5, 25, 10, 30, 0, TimeSpan.Zero).AddTicks(1_234_567);

    private static LogEvent BuildTypedEvent() =>
        LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Information)
            .WithMessage("round-trip")
            .WithTime(SubMsTime)
            .WithProperty("count", 42L)
            .WithProperty("ratio", 3.5d)
            .WithProperty("enabled", true)
            .WithProperty("label", "hello")
            .WithProperty("empty", "")
            .Build();

    private static object? PropertyValue(IReadOnlyList<LogProperty> properties, string name) =>
        properties.First(p => p.Name == name).ResolvedValue;

    // ── Protobuf sink ────────────────────────────────────────────────

    [Fact]
    public void Protobuf_roundtrip_preserves_attribute_types()
    {
        var serializer = new OtlpProtobufLogSerializer(_registry);
        var bytes = serializer.Serialize(BuildTypedEvent());

        var decoded = OtlpProtobufLogDecoder.Decode(bytes, _registry).Single();

        // F1: each attribute keeps its declared type, not stringValue.
        PropertyValue(decoded.Properties, "count").Should().Be(42L);
        PropertyValue(decoded.Properties, "ratio").Should().Be(3.5d);
        PropertyValue(decoded.Properties, "enabled").Should().Be(true);
        PropertyValue(decoded.Properties, "label").Should().Be("hello");
    }

    [Fact]
    public void Protobuf_roundtrip_preserves_sub_millisecond_timestamp()
    {
        var serializer = new OtlpProtobufLogSerializer(_registry);
        var bytes = serializer.Serialize(BuildTypedEvent());

        var decoded = OtlpProtobufLogDecoder.Decode(bytes, _registry).Single();

        // F2: full-tick (100ns) precision survives the round trip exactly.
        decoded.TimeUtc.Should().Be(SubMsTime);
    }

    [Fact]
    public void Protobuf_roundtrip_preserves_empty_string_attribute()
    {
        var serializer = new OtlpProtobufLogSerializer(_registry);
        var bytes = serializer.Serialize(BuildTypedEvent());

        var decoded = OtlpProtobufLogDecoder.Decode(bytes, _registry).Single();

        // F4: an empty-string attribute decodes as an explicit empty string,
        // not as an omitted field that reads back null.
        PropertyValue(decoded.Properties, "empty").Should().Be("");
    }

    // ── JSON sink ────────────────────────────────────────────────────

    private string CaptureJsonPayload(LogEvent evt)
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        var sink = new OtlpJsonLogSink(
            "http://localhost:4318/v1/logs",
            _registry,
            httpClient: client);

        sink.Log(evt);
        return handler.LastRequestBodyString!;
    }

    [Fact]
    public void Json_roundtrip_preserves_attribute_types()
    {
        var json = CaptureJsonPayload(BuildTypedEvent());

        var decoded = OtlpJsonDecoder.Decode(json, _registry).Single();

        // F1: each attribute keeps its declared type, not stringValue.
        PropertyValue(decoded.Properties, "count").Should().Be(42L);
        PropertyValue(decoded.Properties, "ratio").Should().Be(3.5d);
        PropertyValue(decoded.Properties, "enabled").Should().Be(true);
        PropertyValue(decoded.Properties, "label").Should().Be("hello");
    }

    [Fact]
    public void Json_roundtrip_preserves_sub_millisecond_timestamp()
    {
        var json = CaptureJsonPayload(BuildTypedEvent());

        var decoded = OtlpJsonDecoder.Decode(json, _registry).Single();

        // F2: full-tick (100ns) precision survives the round trip exactly.
        decoded.TimeUtc.Should().Be(SubMsTime);
    }

    [Fact]
    public void Json_roundtrip_preserves_empty_string_attribute()
    {
        var json = CaptureJsonPayload(BuildTypedEvent());

        var decoded = OtlpJsonDecoder.Decode(json, _registry).Single();

        // F4: an empty-string attribute decodes as an explicit empty string.
        PropertyValue(decoded.Properties, "empty").Should().Be("");
    }
}
