// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System.Collections.Generic;

namespace Herald.Sinks.Conformance.Tests.Harness;

/// <summary>
/// The reserved-field answer key per format, transcribed from
/// docs/log-formats-reference.md. This is the C# twin of the Go logcreator's
/// <c>canonicalSamples</c> map in conformance_test.go — both reference the
/// same doc, so the .NET sinks and the Go reference emitter share one source
/// of truth.
///
/// <para>
/// Each entry lists only the RESERVED fields an ingester keys off, with the
/// value and JSON kind the doc renders. A sink passes when its output is a
/// superset that matches every reserved field on value AND kind. The doc note
/// "anything outside the reserved set lands as a free-form attribute" is why
/// extra fields are not failures.
/// </para>
///
/// <para>
/// When the reference doc changes, update these entries in the same commit so
/// the doc and the harness never silently disagree — the same discipline the
/// Go conformance test documents.
/// </para>
/// </summary>
public static class CanonicalAnswerKey
{
    // Wall-clock millisecond ISO-8601 the string-timestamp formats render.
    public const string TimestampMillisIso = "2026-05-25T14:32:01.123Z";

    // The canonical instant's Unix epoch, in seconds with a millisecond
    // fraction. This is the SAME instant as TimestampMillisIso, just in the
    // epoch-numeric form that Splunk (and GELF) render. The two forms are
    // derived from one truth: 2026-05-25T14:32:01.123Z. An earlier answer key
    // carried 1764081121.123, which decodes to 2025-11-25 (wrong month) — a
    // base poisoned across every epoch-derived literal. The authoritative
    // values for the canonical instant are:
    //   Unix seconds (ms fraction): 1779719521.123  (this constant)
    //   Unix milliseconds:          1779719521123
    //   Unix nanoseconds (ms floor):1779719521123000000
    //   Unix nanoseconds (full tick):1779719521123456700
    // The string forms are what an ingester's parser sees on the wire, so they
    // are pinned as text rather than recomputed, keeping the answer key a flat
    // declarative table.
    public const string EpochSecondsMillis = "1779719521.123";

    /// <summary>Datadog JSON logs intake (doc #3).</summary>
    public static IReadOnlyList<ReservedField> Datadog { get; } = new[]
    {
        ReservedField.Str("timestamp", TimestampMillisIso),
        ReservedField.Str("status", "info"),
        ReservedField.Str("message", CanonicalEvent.Rendered),
        ReservedField.Str("service", CanonicalEvent.Service),
        ReservedField.Str("hostname", CanonicalEvent.Host),
        // usr.id is a NESTED reserved object carrying a typed number — this is
        // the field that distinguishes Datadog's user-attribute surface from a
        // flat free-form attribute.
        ReservedField.Int("usr.id", CanonicalEvent.UserId),
    };

    /// <summary>
    /// ECS / Elasticsearch (doc #4). NOTE the schema-correct coercions:
    /// <c>user.id</c> is a STRING in ECS by spec, and <c>log.level</c> is a
    /// lowercase string. These are NOT type-loss bugs — the answer key pins
    /// them as strings on purpose.
    /// </summary>
    public static IReadOnlyList<ReservedField> Ecs { get; } = new[]
    {
        ReservedField.Str("@timestamp", TimestampMillisIso, literalKey: true),
        ReservedField.Str("log.level", "info", literalKey: true),
        ReservedField.Str("message", CanonicalEvent.Rendered, literalKey: true),
        ReservedField.Str("ecs.version", "8.11.0", literalKey: true),
        ReservedField.Str("service.name", CanonicalEvent.Service, literalKey: true),
        ReservedField.Str("source.ip", CanonicalEvent.Ip, literalKey: true),
        ReservedField.Str("user.id", "42", literalKey: true), // ECS says string
    };

    /// <summary>
    /// Splunk HEC envelope (doc #10): the reserved fields a Splunk indexer keys
    /// off before it ever parses the nested <c>event</c> payload. <c>time</c> is
    /// the canonical instant in epoch-seconds form; <c>sourcetype</c> is
    /// <c>_json</c> when the sink is constructed with it (the value the doc's
    /// canonical sample shows).
    /// </summary>
    public static IReadOnlyList<ReservedField> SplunkEnvelope { get; } = new[]
    {
        ReservedField.Num("time", EpochSecondsMillis),
        ReservedField.Str("host", CanonicalEvent.Host),
        ReservedField.Str("sourcetype", "_json"),
    };

    /// <summary>
    /// Splunk HEC nested <c>event</c> object (doc #10). Inside <c>event</c>
    /// Splunk has no reserved field NAMES — with <c>sourcetype:_json</c> it
    /// parses the whole object as structured JSON, so these are the fields the
    /// .NET sink actually emits, under the property names Herald.OSS handed it.
    ///
    /// <para>
    /// The doc's canonical sample renders <c>user_id</c>/<c>source_ip</c>
    /// because the Go logcreator's canonical event already carries snake_case
    /// property names. The .NET <see cref="CanonicalEvent"/> carries the raw
    /// Herald names <c>UserId</c> and <c>IP</c>, and the sink emits them
    /// verbatim — it does not rename properties. So the body answer key pins
    /// the genuinely-reserved <c>level</c> (S-1: lowercase key) and
    /// <c>message</c>, plus the typed <c>UserId</c> under its real emitted name
    /// to keep the JSON-number fidelity bar in the answer key. (The dedicated
    /// <c>Splunk_event_body_preserves_property_types</c> test pins the same
    /// typed survival independently.)
    /// </para>
    /// </summary>
    public static IReadOnlyList<ReservedField> SplunkBody { get; } = new[]
    {
        ReservedField.Str("level", "info"),
        ReservedField.Str("message", CanonicalEvent.Rendered),
        ReservedField.Int("UserId", CanonicalEvent.UserId),
    };

    /// <summary>
    /// Loki push (doc #9). The stream labels are low-cardinality only
    /// (service + level); the high-cardinality fields ride in the log line,
    /// not the labels.
    /// </summary>
    public static IReadOnlyList<ReservedField> LokiLabels { get; } = new[]
    {
        ReservedField.Str("service", CanonicalEvent.Service),
        ReservedField.Str("level", "info"),
    };
}
