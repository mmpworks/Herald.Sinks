// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Herald.Sinks.Conformance.Tests.Harness;
using Herald.Sinks.Datadog;
using Herald.Sinks.Elasticsearch;
using Herald.Sinks.Loki;
using Herald.Sinks.Splunk;
using MMP.Herald.Levels;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Conformance.Tests;

/// <summary>
/// Emit-against-reference conformance for the emit-only formats (Herald posts,
/// does not ingest): Datadog, ECS/Elasticsearch, Loki, Splunk. Each sink emits
/// the canonical event; the harness asserts the output is a reserved-field
/// superset that matches the docs/log-formats-reference.md answer key on value
/// AND JSON kind.
///
/// <para>
/// Tests carrying <c>Skip</c> pin a CURRENT FAILURE for Glenn to fix. The Skip
/// reason names the finding ID from the consolidated report. When Glenn applies
/// the fix, he removes the Skip; the assertion then guards the fix as a
/// standing gate. Do not delete a skipped test — it is the executable form of
/// the finding.
/// </para>
/// </summary>
public sealed class EmitReferenceConformanceTests
{
    private readonly ILogLevelRegistry _registry = RegistryHelper.CreateDefault();

    // ── Datadog (doc #3) ─────────────────────────────────────────────

    [Fact]
    public void Datadog_matches_reserved_fields()
    {
        var body = SinkOutputCapture.CaptureBody(
            client => new DatadogLogSink("dd-key", CanonicalEvent.Service,
                hostname: CanonicalEvent.Host, httpClient: client),
            CanonicalEvent.Build());

        // Datadog posts a JSON array; the reserved fields live on the first element.
        using var doc = JsonDocument.Parse(body);
        var first = doc.RootElement[0];

        var result = ConformanceAsserter.Assert("datadog", first, CanonicalAnswerKey.Datadog);
        result.Passed.Should().BeTrue(result.Describe());
    }

    // ── ECS / Elasticsearch (doc #4) ─────────────────────────────────

    [Fact(Skip = "FAIL E-1..E-4: not ECS-shaped (level/level_key/category/nested properties, no dotted fields); @timestamp is 7-digit+offset not ms-Z; level is 'Info' PascalCase not 'info'; no ecs.version/source.ip/user.id. ECS field-shape pending Richard. (E-5 property-type stringification is fixed and guarded by Ecs_properties_preserve_property_types below.)")]
    public void Ecs_matches_reserved_fields()
    {
        var body = SinkOutputCapture.CaptureBody(
            client => new ElasticsearchLogSink("http://localhost:9200", _registry, httpClient: client),
            CanonicalEvent.Build());

        // Elasticsearch bulk body is NDJSON: action line, then document line.
        var documentLine = body.Split('\n')[1];

        var result = ConformanceAsserter.Assert("ecs", documentLine, CanonicalAnswerKey.Ecs);
        result.Passed.Should().BeTrue(result.Describe());
    }

    [Fact]
    public void Ecs_properties_preserve_property_types()
    {
        // E-5 (F1): the nested "properties" object must carry UserId out as a
        // JSON number, not the string "42". This guards the type-dispatch fix
        // independently of the deferred ECS field-shape work (E-1..E-4), the
        // same way Splunk_event_body_preserves_property_types is split from the
        // reserved-field-name assertion above.
        var body = SinkOutputCapture.CaptureBody(
            client => new ElasticsearchLogSink("http://localhost:9200", _registry, httpClient: client),
            CanonicalEvent.Build());

        var documentLine = body.Split('\n')[1];
        using var doc = JsonDocument.Parse(documentLine);
        var properties = doc.RootElement.GetProperty("properties");

        properties.GetProperty("UserId").ValueKind.Should().Be(JsonValueKind.Number);
        properties.GetProperty("UserId").GetInt64().Should().Be(CanonicalEvent.UserId);
        properties.GetProperty("IP").ValueKind.Should().Be(JsonValueKind.String);
        properties.GetProperty("IP").GetString().Should().Be(CanonicalEvent.Ip);
    }

    // ── Loki (doc #9) ────────────────────────────────────────────────

    [Fact]
    public void Loki_stream_labels_match_reserved_fields()
    {
        var body = SinkOutputCapture.CaptureBody(
            client => new LokiLogSink("http://localhost:3100",
                staticLabels: new System.Collections.Generic.Dictionary<string, string>
                {
                    ["service"] = CanonicalEvent.Service,
                },
                httpClient: client),
            CanonicalEvent.Build());

        // Navigate to streams[0].stream — the label object.
        using var doc = JsonDocument.Parse(body);
        var labels = doc.RootElement
            .GetProperty("streams")[0]
            .GetProperty("stream");

        var result = ConformanceAsserter.Assert("loki-labels", labels, CanonicalAnswerKey.LokiLabels);
        result.Passed.Should().BeTrue(result.Describe());
    }

    [Fact]
    public void Loki_timestamp_preserves_nanosecond_precision()
    {
        var body = SinkOutputCapture.CaptureBody(
            client => new LokiLogSink("http://localhost:3100", httpClient: client),
            CanonicalEvent.Build());

        using var doc = JsonDocument.Parse(body);
        var unixNanoString = doc.RootElement
            .GetProperty("streams")[0]
            .GetProperty("values")[0][0]
            .GetString();

        // F2: the canonical instant carries sub-ms ticks. A faithful nanosecond
        // timestamp ends in the tick remainder, not three zeros from a ms floor.
        var expectedNanos =
            (CanonicalEvent.Instant - System.DateTimeOffset.UnixEpoch).Ticks * 100L;
        unixNanoString.Should().Be(
            expectedNanos.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    // ── Splunk HEC (doc #10) ─────────────────────────────────────────

    [Fact(Skip = "FAIL (harness/answer-key, NOT S-1): SplunkEnvelope answer key 'time' is 1764081121.123 (2025-11-25) but the canonical instant is 2026-05-25 -> 1779719521.123; and 'sourcetype' _json is expected but the sink only emits sourcetype when constructed with one. Both are envelope-level harness defects outside the S-1 finding. S-1 (event.level lowercase) is fixed and guarded by Splunk_event_body_reserved_level below. Hand back to harness author.")]
    public void Splunk_event_body_matches_reserved_fields()
    {
        var body = SinkOutputCapture.CaptureBody(
            client => new SplunkHecLogSink("http://localhost:8088", "splunk-token",
                host: CanonicalEvent.Host, httpClient: client),
            CanonicalEvent.Build());

        // HEC posts JSON objects; parse the first, then navigate into event.
        using var doc = JsonDocument.Parse(body.Split('\n')[0]);
        var root = doc.RootElement;
        var eventObj = root.GetProperty("event");

        var envelope = ConformanceAsserter.Assert("splunk-envelope", root, CanonicalAnswerKey.SplunkEnvelope);
        var bodyResult = ConformanceAsserter.Assert("splunk-event", eventObj, CanonicalAnswerKey.SplunkBody);

        envelope.Passed.Should().BeTrue(envelope.Describe());
        bodyResult.Passed.Should().BeTrue(bodyResult.Describe());
    }

    [Fact]
    public void Splunk_event_body_reserved_level()
    {
        // S-1: the reserved Splunk event.level field must be the lowercase
        // level key ("info"), not the uppercase severity DisplayName. This
        // guards the S-1 fix independently of the envelope-level harness
        // defects pinned in Splunk_event_body_matches_reserved_fields above.
        var body = SinkOutputCapture.CaptureBody(
            client => new SplunkHecLogSink("http://localhost:8088", "splunk-token",
                host: CanonicalEvent.Host, httpClient: client),
            CanonicalEvent.Build());

        using var doc = JsonDocument.Parse(body.Split('\n')[0]);
        var eventObj = doc.RootElement.GetProperty("event");

        eventObj.GetProperty("level").ValueKind.Should().Be(JsonValueKind.String);
        eventObj.GetProperty("level").GetString().Should().Be("info");
    }

    [Fact]
    public void Splunk_event_body_preserves_property_types()
    {
        // This PASSES today: Splunk's WriteValue does correct type-dispatch, so
        // UserId rides as a JSON number inside event. It is the reserved-field
        // NAME (severity vs level) that fails, pinned separately above.
        var body = SinkOutputCapture.CaptureBody(
            client => new SplunkHecLogSink("http://localhost:8088", "splunk-token",
                host: CanonicalEvent.Host, httpClient: client),
            CanonicalEvent.Build());

        using var doc = JsonDocument.Parse(body.Split('\n')[0]);
        var eventObj = doc.RootElement.GetProperty("event");

        eventObj.GetProperty("UserId").ValueKind.Should().Be(JsonValueKind.Number);
        eventObj.GetProperty("UserId").GetInt64().Should().Be(CanonicalEvent.UserId);
    }
}
