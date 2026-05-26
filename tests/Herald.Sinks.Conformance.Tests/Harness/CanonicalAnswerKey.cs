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
    /// Splunk HEC (doc #10). The reserved envelope plus the typed body. The
    /// doc renders <c>user_id</c> as a NUMBER inside the nested <c>event</c>
    /// object; the harness navigates into <c>event</c> before asserting these.
    /// </summary>
    public static IReadOnlyList<ReservedField> SplunkEnvelope { get; } = new[]
    {
        ReservedField.Num("time", "1764081121.123"),
        ReservedField.Str("host", CanonicalEvent.Host),
        ReservedField.Str("sourcetype", "_json"),
    };

    /// <summary>Splunk HEC nested <c>event</c> object (doc #10).</summary>
    public static IReadOnlyList<ReservedField> SplunkBody { get; } = new[]
    {
        ReservedField.Str("level", "info"),
        ReservedField.Str("message", CanonicalEvent.Rendered),
        ReservedField.Int("user_id", CanonicalEvent.UserId),
        ReservedField.Str("source_ip", CanonicalEvent.Ip),
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
