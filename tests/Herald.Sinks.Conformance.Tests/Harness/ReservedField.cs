// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System.Text.Json;

namespace Herald.Sinks.Conformance.Tests.Harness;

/// <summary>
/// One reserved field in a format's answer key: the JSON path the ingester
/// keys off, the value it must carry, and the JSON value KIND it must carry
/// the value AS.
///
/// <para>
/// The kind is the load-bearing half of the fidelity bar. A field that
/// matches on value but not on kind is the F1 defect: a number flattened to
/// a string still "reads right" to a human but the ingester's typed query
/// surface (Datadog facets, OTLP intValue, ECS numeric mappings) breaks.
/// </para>
///
/// <para>
/// <see cref="Path"/> is a dot-delimited path into the JSON object
/// (<c>usr.id</c> walks into the nested <c>usr</c> object). It is NOT a
/// dotted field NAME — formats that use literal dotted keys (ECS
/// <c>service.name</c>, OTLP <c>log.level</c>) are matched with
/// <see cref="LiteralKey"/> set so the asserter looks up the whole dotted
/// string as one property rather than walking into sub-objects.
/// </para>
/// </summary>
public sealed record ReservedField(
    string Path,
    string ExpectedRaw,
    JsonValueKind ExpectedKind,
    bool LiteralKey = false)
{
    /// <summary>A string-valued reserved field.</summary>
    public static ReservedField Str(string path, string value, bool literalKey = false) =>
        new(path, value, JsonValueKind.String, literalKey);

    /// <summary>An integer-valued reserved field (JSON number, no decimal point).</summary>
    public static ReservedField Int(string path, long value, bool literalKey = false) =>
        new(path, value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonValueKind.Number, literalKey);

    /// <summary>A floating-point reserved field (JSON number).</summary>
    public static ReservedField Num(string path, string rawNumber, bool literalKey = false) =>
        new(path, rawNumber, JsonValueKind.Number, literalKey);

    /// <summary>A boolean-valued reserved field.</summary>
    public static ReservedField Bool(string path, bool value, bool literalKey = false) =>
        new(path, value ? "true" : "false",
            value ? JsonValueKind.True : JsonValueKind.False, literalKey);
}
