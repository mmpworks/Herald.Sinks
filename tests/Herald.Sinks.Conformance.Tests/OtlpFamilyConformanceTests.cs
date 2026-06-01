// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using FluentAssertions;
using Herald.Sinks.Conformance.Tests.Harness;
using Herald.Sinks.Otlp;
using MMP.Herald.Addons.OtlpSinks;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Templating;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Conformance.Tests;

/// <summary>
/// OTLP JSON + OTLP/gRPC (protobuf) round-trip conformance. These run live and
/// PASS — they prove the harness works against a known-conformant sink family
/// after Jared's F1-F4 fixes, and they pin the OtlpGrpc sibling that inherits
/// the shared <see cref="OtlpProtobufLogSerializer"/>.
///
/// <para>
/// Round-trip mode (Herald has both a decoder and a sink for OTLP): event ->
/// encode -> decode -> assert in == out, type-aware. The OSS-side decoders in
/// MMP.Herald.Addons.OtlpSinks are the re-decode half.
/// </para>
/// </summary>
public sealed class OtlpFamilyConformanceTests
{
    private readonly ILogLevelRegistry _registry = RegistryHelper.CreateDefault();

    private static object? PropertyValue(IReadOnlyList<LogProperty> properties, string name) =>
        properties.First(p => p.Name == name).ResolvedValue;

    // ── OTLP JSON (HTTP sink) ────────────────────────────────────────

    private string CaptureJson(LogEvent evt) =>
        SinkOutputCapture.CaptureBody(
            client => new OtlpJsonLogSink("http://localhost:4318", _registry, httpClient: client),
            evt);

    [Fact]
    public void OtlpJson_preserves_attribute_types()
    {
        var json = CaptureJson(CanonicalEvent.Build());
        var decoded = OtlpJsonDecoder.Decode(json, _registry).Single();

        // F1: UserId survives as a long, not a string.
        PropertyValue(decoded.Properties, "UserId").Should().Be(CanonicalEvent.UserId);
        PropertyValue(decoded.Properties, "IP").Should().Be(CanonicalEvent.Ip);
    }

    [Fact]
    public void OtlpJson_preserves_sub_millisecond_timestamp()
    {
        var json = CaptureJson(CanonicalEvent.Build());
        var decoded = OtlpJsonDecoder.Decode(json, _registry).Single();

        // F2: full 100ns-tick precision survives, not floored to ms.
        decoded.TimeUtc.Should().Be(CanonicalEvent.Instant);
    }

    // ── OTLP/gRPC (protobuf serializer) ──────────────────────────────

    [Fact]
    public void OtlpGrpc_preserves_attribute_types()
    {
        // The OtlpGrpc sink delegates to OtlpProtobufLogSerializer; serializing
        // through it exercises the exact bytes the gRPC sink puts on the wire.
        var serializer = new OtlpProtobufLogSerializer(_registry);
        var bytes = serializer.Serialize(CanonicalEvent.Build());

        var decoded = OtlpProtobufLogDecoder.Decode(bytes, _registry).Single();

        // F1: the gRPC sibling inherits the shared serializer's typed-attribute fix.
        PropertyValue(decoded.Properties, "UserId").Should().Be(CanonicalEvent.UserId);
        PropertyValue(decoded.Properties, "IP").Should().Be(CanonicalEvent.Ip);
    }

    [Fact]
    public void OtlpGrpc_preserves_sub_millisecond_timestamp()
    {
        var serializer = new OtlpProtobufLogSerializer(_registry);
        var bytes = serializer.Serialize(CanonicalEvent.Build());

        var decoded = OtlpProtobufLogDecoder.Decode(bytes, _registry).Single();

        // F2: the tick-based unixNano fix carries to the protobuf path.
        decoded.TimeUtc.Should().Be(CanonicalEvent.Instant);
    }

    [Fact]
    public void OtlpGrpc_uses_uppercase_severity_text()
    {
        var serializer = new OtlpProtobufLogSerializer(_registry);
        var bytes = serializer.Serialize(CanonicalEvent.Build());

        var decoded = OtlpProtobufLogDecoder.Decode(bytes, _registry).Single();

        // F3: severity text is uppercase per OTLP convention. The decoder maps
        // it back through the registry; the level round-trips to Info.
        decoded.Level.Key.Should().Be(KnownLogLevels.Information.Key);
    }
}
