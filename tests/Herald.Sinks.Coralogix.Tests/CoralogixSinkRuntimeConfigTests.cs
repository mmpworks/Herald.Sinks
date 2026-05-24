// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using FluentAssertions;
using Herald.Sinks.Coralogix.Providers;
using MMP.Herald.Configuration.Runtime;
using Xunit;

namespace Herald.Sinks.Coralogix.Tests;

/// <summary>
/// Translation-faithfulness specs for
/// <see cref="CoralogixSinkRuntimeConfig"/>. Confirms the property-bag
/// path (configuration-coralogix.mmpform) supplies the four sink-ctor
/// fields, that the legacy slot fallback works for the three that
/// have a sensible legacy mapping, and that the provider rejects
/// definitions missing any required field with a named
/// ArgumentException.
/// </summary>
public sealed class CoralogixSinkRuntimeConfigTests
{
    // ── Bag → Resolved ──────────────────────────────────────────────

    [Fact]
    public void Reads_all_four_keys_from_property_bag_when_present()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "coralogix",
            Kind: "coralogix",
            Properties: new Dictionary<string, object?>
            {
                ["endpoint"]         = "https://ingress.coralogix.com/api/v1/logs",
                ["private_key"]      = "cr-key",
                ["application_name"] = "herald-app",
                ["subsystem_name"]   = "ingest"
            });

        var resolved = CoralogixSinkRuntimeConfig.From(def);

        resolved.Endpoint.Should().Be("https://ingress.coralogix.com/api/v1/logs");
        resolved.PrivateKey.Should().Be("cr-key");
        resolved.ApplicationName.Should().Be("herald-app");
        resolved.SubsystemName.Should().Be("ingest");
    }

    [Fact]
    public void Bag_wins_over_legacy_slots_when_both_are_set()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "coralogix",
            Kind: "coralogix",
            Uri:   "https://legacy.example.com",
            Alias: "legacy-key",
            Host:  "legacy-app",
            Properties: new Dictionary<string, object?>
            {
                ["endpoint"]         = "https://bag.example.com",
                ["private_key"]      = "bag-key",
                ["application_name"] = "bag-app",
                ["subsystem_name"]   = "bag-sub"
            });

        var resolved = CoralogixSinkRuntimeConfig.From(def);

        resolved.Endpoint.Should().Be("https://bag.example.com");
        resolved.PrivateKey.Should().Be("bag-key");
        resolved.ApplicationName.Should().Be("bag-app");
        resolved.SubsystemName.Should().Be("bag-sub");
    }

    [Fact]
    public void Subsystem_has_no_legacy_slot_so_must_come_from_bag()
    {
        // Coralogix's three string slots are spoken for (Uri/Alias/Host
        // → endpoint/private_key/application_name). subsystem_name has
        // no legacy mapping; older deployments that wanted Coralogix
        // had to use the code-first ctor. The provider's required-field
        // guard catches the missing value with a named message.
        var def = new LoggingRuntimeSinkDefinition(
            Name: "coralogix",
            Kind: "coralogix",
            Uri:   "https://legacy.example.com",
            Alias: "legacy-key",
            Host:  "legacy-app");

        var resolved = CoralogixSinkRuntimeConfig.From(def);

        resolved.SubsystemName.Should().BeNull();
    }

    // ── Provider end-to-end ─────────────────────────────────────────

    [Fact]
    public void Provider_creates_sink_from_bag_only_definition()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "coralogix",
            Kind: "coralogix",
            Properties: new Dictionary<string, object?>
            {
                ["endpoint"]         = "https://bag.example.com",
                ["private_key"]      = "bag-key",
                ["application_name"] = "bag-app",
                ["subsystem_name"]   = "bag-sub"
            });

        var sink = new CoralogixLogSinkProvider().CreateSink(def, null!, null!);
        sink.Should().NotBeNull();
        sink.Should().BeOfType<CoralogixLogSink>();
    }

    [Fact]
    public void Provider_throws_when_private_key_is_missing()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "coralogix",
            Kind: "coralogix",
            Properties: new Dictionary<string, object?>
            {
                ["application_name"] = "app",
                ["subsystem_name"]   = "sub"
            });

        var act = () => new CoralogixLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*private_key*");
    }

    [Fact]
    public void Provider_throws_when_application_name_is_missing()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "coralogix",
            Kind: "coralogix",
            Properties: new Dictionary<string, object?>
            {
                ["private_key"]    = "key",
                ["subsystem_name"] = "sub"
            });

        var act = () => new CoralogixLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*application_name*");
    }

    [Fact]
    public void Provider_throws_when_subsystem_name_is_missing()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "coralogix",
            Kind: "coralogix",
            Properties: new Dictionary<string, object?>
            {
                ["private_key"]      = "key",
                ["application_name"] = "app"
            });

        var act = () => new CoralogixLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*subsystem_name*");
    }
}
