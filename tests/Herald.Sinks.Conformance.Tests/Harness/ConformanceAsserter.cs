// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace Herald.Sinks.Conformance.Tests.Harness;

/// <summary>
/// Type-aware field-equality engine. Given a sink's emitted JSON and a list of
/// <see cref="ReservedField"/> answer-key entries, it reports every reserved
/// field that is missing, type-mismatched, or value-mismatched.
///
/// <para>
/// This is the reusable core of the conformance harness. It has no knowledge
/// of any specific sink or format — callers supply the JSON and the answer
/// key. Echo can plug negative vectors in by supplying a mutated answer key
/// (e.g. a field the sink MUST drop) and asserting the matching failure class.
/// </para>
///
/// <para>
/// The asserter walks <see cref="ReservedField.Path"/> as a dot path into
/// nested objects unless <see cref="ReservedField.LiteralKey"/> is set, in
/// which case the whole dotted string is one property name. This is the only
/// branch in the walk; everything else is a flat compare on the resolved
/// element's kind and value.
/// </para>
/// </summary>
public static class ConformanceAsserter
{
    public static ConformanceResult Assert(
        string format,
        string emittedJson,
        IReadOnlyList<ReservedField> answerKey)
    {
        using var doc = JsonDocument.Parse(emittedJson);
        return Assert(format, doc.RootElement, answerKey);
    }

    /// <summary>
    /// Assert against an already-parsed element. Used when the sink output is
    /// not a bare object (Loki's log-line-within-an-array, Splunk's nested
    /// <c>event</c>) and the caller has navigated to the object that carries
    /// the reserved fields.
    /// </summary>
    public static ConformanceResult Assert(
        string format,
        JsonElement root,
        IReadOnlyList<ReservedField> answerKey)
    {
        var failures = new List<FieldFailure>();
        foreach (var field in answerKey)
        {
            CheckField(root, field, failures);
        }
        return new ConformanceResult(format, failures);
    }

    // Cognitive complexity stays low by splitting "resolve the element" from
    // "compare the element." Resolve handles the path walk and the absent
    // case; Compare handles kind + value once an element is in hand.
    private static void CheckField(JsonElement root, ReservedField field, List<FieldFailure> failures)
    {
        if (!TryResolve(root, field, out var element))
        {
            failures.Add(new FieldFailure(field.Path, FailureClass.Missing,
                Describe(field), "<absent>"));
            return;
        }

        if (element.ValueKind == JsonValueKind.Null)
        {
            failures.Add(new FieldFailure(field.Path, FailureClass.Missing,
                Describe(field), "null"));
            return;
        }

        if (element.ValueKind != field.ExpectedKind)
        {
            failures.Add(new FieldFailure(field.Path, FailureClass.TypeMismatch,
                $"{field.ExpectedKind} {field.ExpectedRaw}",
                $"{element.ValueKind} {Raw(element)}"));
            return;
        }

        if (!ValueMatches(element, field))
        {
            failures.Add(new FieldFailure(field.Path, FailureClass.ValueMismatch,
                field.ExpectedRaw, Raw(element)));
        }
    }

    private static bool TryResolve(JsonElement root, ReservedField field, out JsonElement element)
    {
        if (field.LiteralKey)
        {
            return root.TryGetProperty(field.Path, out element);
        }

        element = root;
        foreach (var segment in field.Path.Split('.'))
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty(segment, out element))
            {
                element = default;
                return false;
            }
        }
        return true;
    }

    // Numbers compare by parsed value so 42 == 42.0 only when the answer key
    // says so; the kind check above already separated int-shaped from
    // float-shaped intent. For numbers we compare the canonical raw text the
    // writer produced against the expected raw, which is what an ingester's
    // parser sees on the wire.
    private static bool ValueMatches(JsonElement element, ReservedField field) =>
        field.ExpectedKind switch
        {
            JsonValueKind.String => element.GetString() == field.ExpectedRaw,
            JsonValueKind.Number => NumberMatches(element, field.ExpectedRaw),
            JsonValueKind.True or JsonValueKind.False => true, // kind already pinned the bool
            _ => Raw(element) == field.ExpectedRaw,
        };

    private static bool NumberMatches(JsonElement element, string expectedRaw)
    {
        var actualRaw = element.GetRawText();
        if (actualRaw == expectedRaw)
        {
            return true;
        }

        // Tolerate decimal-formatting differences (42 vs 42.0) by comparing
        // parsed doubles; the kind check guarantees both are JSON numbers.
        return double.TryParse(actualRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var a)
            && double.TryParse(expectedRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var e)
            && a == e;
    }

    private static string Raw(JsonElement element) =>
        element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? ""
            : element.GetRawText();

    private static string Describe(ReservedField field) =>
        $"{field.ExpectedKind} {field.ExpectedRaw}";
}
