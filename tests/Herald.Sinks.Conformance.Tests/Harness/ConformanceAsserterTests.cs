// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using FluentAssertions;
using Xunit;

namespace Herald.Sinks.Conformance.Tests.Harness;

/// <summary>
/// Self-tests for the assertion engine. A conformance gate is only as
/// trustworthy as the asserter behind it — these prove it catches each
/// failure class and admits each legitimate pass. This is also the surface
/// Echo extends with negative vectors: a negative vector is just an answer
/// key the output is expected to FAIL, asserted via the matching failure class.
/// </summary>
public sealed class ConformanceAsserterTests
{
    [Fact]
    public void Passes_when_reserved_fields_match_value_and_kind()
    {
        const string json = """{"status":"info","usr":{"id":42}}""";
        var key = new[]
        {
            ReservedField.Str("status", "info"),
            ReservedField.Int("usr.id", 42),
        };

        ConformanceAsserter.Assert("t", json, key).Passed.Should().BeTrue();
    }

    [Fact]
    public void Allows_extra_fields_beyond_the_reserved_set()
    {
        const string json = """{"status":"info","messageTemplate":"x","category":"App"}""";
        var key = new[] { ReservedField.Str("status", "info") };

        ConformanceAsserter.Assert("t", json, key).Passed.Should().BeTrue();
    }

    [Fact]
    public void Flags_type_mismatch_when_number_is_stringified()
    {
        // The F1 defect: 42 emitted as "42".
        const string json = """{"usr":{"id":"42"}}""";
        var key = new[] { ReservedField.Int("usr.id", 42) };

        var result = ConformanceAsserter.Assert("t", json, key);
        result.Passed.Should().BeFalse();
        result.Failures.Should().ContainSingle()
            .Which.Class.Should().Be(FailureClass.TypeMismatch);
    }

    [Fact]
    public void Flags_value_mismatch_when_casing_drifts()
    {
        // The F3 defect: "INFO" where "info" is reserved.
        const string json = """{"status":"INFO"}""";
        var key = new[] { ReservedField.Str("status", "info") };

        var result = ConformanceAsserter.Assert("t", json, key);
        result.Failures.Should().ContainSingle()
            .Which.Class.Should().Be(FailureClass.ValueMismatch);
    }

    [Fact]
    public void Flags_missing_when_field_absent()
    {
        const string json = """{"status":"info"}""";
        var key = new[] { ReservedField.Int("usr.id", 42) };

        var result = ConformanceAsserter.Assert("t", json, key);
        result.Failures.Should().ContainSingle()
            .Which.Class.Should().Be(FailureClass.Missing);
    }

    [Fact]
    public void Flags_missing_when_field_present_but_null()
    {
        // The F4 defect: a present-but-zero/empty value omitted to null.
        const string json = """{"hostname":null}""";
        var key = new[] { ReservedField.Str("hostname", "web-01") };

        var result = ConformanceAsserter.Assert("t", json, key);
        result.Failures.Should().ContainSingle()
            .Which.Class.Should().Be(FailureClass.Missing);
    }

    [Fact]
    public void Literal_key_matches_dotted_field_name_without_walking()
    {
        // ECS uses literal dotted keys: "service.name" is one property, not a
        // walk into service -> name.
        const string json = """{"service.name":"herald-demo"}""";
        var key = new[] { ReservedField.Str("service.name", "herald-demo", literalKey: true) };

        ConformanceAsserter.Assert("t", json, key).Passed.Should().BeTrue();
    }

    [Fact]
    public void Number_match_tolerates_decimal_formatting()
    {
        const string json = """{"time":1764081121.123}""";
        var key = new[] { ReservedField.Num("time", "1764081121.123") };

        ConformanceAsserter.Assert("t", json, key).Passed.Should().BeTrue();
    }
}
