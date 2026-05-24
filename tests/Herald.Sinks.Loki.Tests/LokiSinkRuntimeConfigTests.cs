// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using FluentAssertions;
using Herald.Sinks.Loki.Providers;
using MMP.Herald.Configuration.Runtime;
using Xunit;

namespace Herald.Sinks.Loki.Tests;

/// <summary>
/// Translation-faithfulness specs for
/// <see cref="LokiSinkRuntimeConfig"/>. Confirms the property-bag
/// path (configuration-loki.mmpform) wins over the legacy
/// Uri/Alias slots, and that the provider rejects definitions
/// missing the required endpoint with a named ArgumentException.
/// </summary>
public sealed class LokiSinkRuntimeConfigTests
{
    // ── Bag → Resolved ──────────────────────────────────────────────

    [Fact]
    public void Reads_endpoint_and_bearer_from_property_bag_when_present()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "loki",
            Kind: "loki",
            Properties: new Dictionary<string, object?>
            {
                ["endpoint"]     = "https://logs-prod.grafana.net",
                ["bearer_token"] = "tok-from-bag"
            });

        var resolved = LokiSinkRuntimeConfig.From(def);

        resolved.Endpoint.Should().Be("https://logs-prod.grafana.net");
        resolved.BearerToken.Should().Be("tok-from-bag");
    }

    [Fact]
    public void Bag_wins_over_legacy_slots_when_both_are_set()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "loki",
            Kind: "loki",
            Uri:   "https://legacy.example.com",
            Alias: "legacy-token",
            Properties: new Dictionary<string, object?>
            {
                ["endpoint"]     = "https://bag.example.com",
                ["bearer_token"] = "bag-token"
            });

        var resolved = LokiSinkRuntimeConfig.From(def);

        resolved.Endpoint.Should().Be("https://bag.example.com");
        resolved.BearerToken.Should().Be("bag-token");
    }

    [Fact]
    public void Falls_back_to_legacy_slots_when_bag_is_null()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "loki",
            Kind: "loki",
            Uri:   "https://legacy.example.com",
            Alias: "legacy-token");

        var resolved = LokiSinkRuntimeConfig.From(def);

        resolved.Endpoint.Should().Be("https://legacy.example.com");
        resolved.BearerToken.Should().Be("legacy-token");
    }

    [Fact]
    public void Falls_back_to_legacy_when_bag_carries_empty_values()
    {
        // Empty bag values match a form field the operator left blank —
        // they should not stomp the matching legacy slot.
        var def = new LoggingRuntimeSinkDefinition(
            Name: "loki",
            Kind: "loki",
            Uri:   "https://legacy.example.com",
            Alias: "legacy-token",
            Properties: new Dictionary<string, object?>
            {
                ["endpoint"]     = "",
                ["bearer_token"] = ""
            });

        var resolved = LokiSinkRuntimeConfig.From(def);

        resolved.Endpoint.Should().Be("https://legacy.example.com");
        resolved.BearerToken.Should().Be("legacy-token");
    }

    [Fact]
    public void Returns_null_bearer_when_neither_source_supplies_it()
    {
        // Self-hosted Loki behind a reverse proxy can leave bearer null;
        // only endpoint is required.
        var def = new LoggingRuntimeSinkDefinition(
            Name: "loki",
            Kind: "loki",
            Uri: "http://loki:3100");

        var resolved = LokiSinkRuntimeConfig.From(def);

        resolved.Endpoint.Should().Be("http://loki:3100");
        resolved.BearerToken.Should().BeNull();
    }

    // ── Provider end-to-end ─────────────────────────────────────────

    [Fact]
    public void Provider_creates_sink_from_bag_only_definition()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "loki",
            Kind: "loki",
            Properties: new Dictionary<string, object?>
            {
                ["endpoint"]     = "https://bag.example.com",
                ["bearer_token"] = "bag-token"
            });

        var sink = new LokiLogSinkProvider().CreateSink(def, null!, null!);
        sink.Should().NotBeNull();
        sink.Should().BeOfType<LokiLogSink>();
    }

    [Fact]
    public void Provider_creates_sink_from_legacy_only_definition()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "loki",
            Kind: "loki",
            Uri:   "https://legacy.example.com",
            Alias: "legacy-token");

        var sink = new LokiLogSinkProvider().CreateSink(def, null!, null!);
        sink.Should().NotBeNull();
    }

    [Fact]
    public void Provider_throws_when_endpoint_missing_from_both_sources()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "loki",
            Kind: "loki",
            Alias: "token-only");

        var act = () => new LokiLogSinkProvider().CreateSink(def, null!, null!);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*endpoint*");
    }
}
