// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using FluentAssertions;
using Herald.Sinks.Datadog.Providers;
using MMP.Herald.Configuration.Runtime;
using Xunit;

namespace Herald.Sinks.Datadog.Tests;

/// <summary>
/// Translation-faithfulness specs for
/// <see cref="DatadogSinkRuntimeConfig"/>. Confirms the property-bag
/// path (configuration-datadog.mmpform) wins over the legacy
/// Uri/Host/Alias slots, and that the provider rejects definitions
/// missing required fields with a named ArgumentException.
/// </summary>
public sealed class DatadogSinkRuntimeConfigTests
{
    // ── Bag → Resolved ──────────────────────────────────────────────

    [Fact]
    public void Reads_all_three_keys_from_property_bag_when_present()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "dd",
            Kind: "datadog",
            Properties: new Dictionary<string, object?>
            {
                ["endpoint"] = "https://http-intake.logs.datadoghq.eu",
                ["api_key"]  = "dd-key-bag",
                ["service"]  = "service-from-bag"
            });

        var resolved = DatadogSinkRuntimeConfig.From(def);

        resolved.Endpoint.Should().Be("https://http-intake.logs.datadoghq.eu");
        resolved.ApiKey.Should().Be("dd-key-bag");
        resolved.Service.Should().Be("service-from-bag");
    }

    [Fact]
    public void Bag_wins_over_legacy_slots_when_both_are_set()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "dd",
            Kind: "datadog",
            Uri:   "https://legacy.example.com",
            Alias: "legacy-key",
            Host:  "legacy-service",
            Properties: new Dictionary<string, object?>
            {
                ["endpoint"] = "https://bag.example.com",
                ["api_key"]  = "bag-key",
                ["service"]  = "bag-service"
            });

        var resolved = DatadogSinkRuntimeConfig.From(def);

        resolved.Endpoint.Should().Be("https://bag.example.com");
        resolved.ApiKey.Should().Be("bag-key");
        resolved.Service.Should().Be("bag-service");
    }

    [Fact]
    public void Falls_back_to_legacy_slots_when_bag_is_null()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "dd",
            Kind: "datadog",
            Uri:   "https://legacy.example.com",
            Alias: "legacy-key",
            Host:  "legacy-service");

        var resolved = DatadogSinkRuntimeConfig.From(def);

        resolved.Endpoint.Should().Be("https://legacy.example.com");
        resolved.ApiKey.Should().Be("legacy-key");
        resolved.Service.Should().Be("legacy-service");
    }

    [Fact]
    public void Falls_back_to_legacy_when_bag_carries_empty_values()
    {
        // An empty-string bag entry mirrors a form field the operator
        // left blank — that should not stomp the matching legacy slot.
        var def = new LoggingRuntimeSinkDefinition(
            Name: "dd",
            Kind: "datadog",
            Uri:   "https://legacy.example.com",
            Alias: "legacy-key",
            Host:  "legacy-service",
            Properties: new Dictionary<string, object?>
            {
                ["endpoint"] = "",
                ["api_key"]  = "",
                ["service"]  = ""
            });

        var resolved = DatadogSinkRuntimeConfig.From(def);

        resolved.Endpoint.Should().Be("https://legacy.example.com");
        resolved.ApiKey.Should().Be("legacy-key");
        resolved.Service.Should().Be("legacy-service");
    }

    [Fact]
    public void Returns_null_fields_when_no_source_supplies_them()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "dd",
            Kind: "datadog");

        var resolved = DatadogSinkRuntimeConfig.From(def);

        resolved.Endpoint.Should().BeNull();
        resolved.ApiKey.Should().BeNull();
        resolved.Service.Should().BeNull();
    }

    // ── Provider end-to-end ─────────────────────────────────────────

    [Fact]
    public void Provider_creates_sink_from_bag_only_definition()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "dd",
            Kind: "datadog",
            Properties: new Dictionary<string, object?>
            {
                ["endpoint"] = "https://bag.example.com",
                ["api_key"]  = "bag-key",
                ["service"]  = "bag-service",
                // batch_size=1 keeps the provider on the pass-through path so
                // this wiring test sees the bare sink, not the batching wrapper.
                ["batch_size"] = 1
            });

        var sink = new DatadogLogSinkProvider().CreateSink(def, null!, null!);
        sink.Should().NotBeNull();
        sink.Should().BeOfType<DatadogLogSink>();
    }

    [Fact]
    public void Provider_creates_sink_from_legacy_only_definition()
    {
        // Deployments still on pre-v2 dashboard JSON keep working —
        // the provider never reaches the bag-required path when the
        // bag is null/empty and legacy slots carry the values.
        var def = new LoggingRuntimeSinkDefinition(
            Name: "dd",
            Kind: "datadog",
            Uri:   "https://legacy.example.com",
            Alias: "legacy-key",
            Host:  "legacy-service");

        var sink = new DatadogLogSinkProvider().CreateSink(def, null!, null!);
        sink.Should().NotBeNull();
    }

    [Fact]
    public void Provider_throws_when_api_key_missing_from_both_sources()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "dd",
            Kind: "datadog",
            Host: "service-only");

        var act = () => new DatadogLogSinkProvider().CreateSink(def, null!, null!);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*api_key*");
    }

    [Fact]
    public void Provider_throws_when_service_missing_from_both_sources()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "dd",
            Kind: "datadog",
            Alias: "key-only");

        var act = () => new DatadogLogSinkProvider().CreateSink(def, null!, null!);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*service*");
    }
}
