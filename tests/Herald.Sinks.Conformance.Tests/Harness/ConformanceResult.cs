// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace Herald.Sinks.Conformance.Tests.Harness;

/// <summary>
/// Why a reserved field failed the fidelity bar. The class is reported per
/// finding so a sweep can group failures by failure class (the F1-F4 buckets
/// from the OTLP work) rather than by sink.
/// </summary>
public enum FailureClass
{
    /// <summary>Reserved field absent from the sink output (or present-but-null when a value was expected) — F4.</summary>
    Missing,

    /// <summary>Field present with the right value but the wrong JSON kind — F1 type-flattening.</summary>
    TypeMismatch,

    /// <summary>Field present with the right kind but the wrong value — includes F2 timestamp truncation and F3 casing drift.</summary>
    ValueMismatch,
}

/// <summary>One reserved-field failure: enough detail for Glenn to act without re-deriving.</summary>
public sealed record FieldFailure(
    string Path,
    FailureClass Class,
    string Expected,
    string Actual);

/// <summary>
/// The outcome of asserting one sink's output against one answer key. A pass
/// has an empty <see cref="Failures"/> list. Extra fields in the output are
/// never failures — the bar is reserved-field SUPERSET, because every sink
/// deliberately emits more than the canonical sample (messageTemplate,
/// category, ...) and the ingester routes the surplus to free-form attributes.
/// </summary>
public sealed record ConformanceResult(string Format, IReadOnlyList<FieldFailure> Failures)
{
    public bool Passed => Failures.Count == 0;

    public string Describe()
    {
        if (Passed)
        {
            return $"{Format}: PASS";
        }

        var lines = Failures.Select(f =>
            $"  [{f.Class}] {f.Path}: expected {f.Expected}, got {f.Actual}");
        return $"{Format}: FAIL ({Failures.Count})\n" + string.Join("\n", lines);
    }
}
