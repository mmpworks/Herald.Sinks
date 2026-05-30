// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using FluentAssertions;
using Herald.Sinks.Syslog;
using Herald.Sinks.Syslog.Providers;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Levels;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Syslog.Tests;

/// <summary>
/// Translation-faithfulness specs for
/// <see cref="SyslogSinkRuntimeConfig"/>, the provider's required-
/// field guard, and the BLOCKER fix — Herald properties now appear
/// in the RFC 5424 STRUCTURED-DATA block instead of being dropped
/// silently.
/// </summary>
public sealed class SyslogSinkRuntimeConfigTests
{
    private static readonly DateTimeOffset FixedTime =
        new(2025, 4, 15, 13, 45, 6, 789, TimeSpan.Zero);

    // ── Bag → Resolved ──────────────────────────────────────────────

    [Fact]
    public void Reads_all_ten_keys_from_property_bag_when_present()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "syslog",
            Kind: "syslog",
            Properties: new Dictionary<string, object?>
            {
                ["host"]                    = "collector.example.com",
                ["port"]                    = 6514,
                ["transport"]               = "tcp",
                ["format"]                  = "rfc5424",
                ["facility"]                = "local3",
                ["app_name"]                = "billing-api",
                ["process_id"]              = "12345",
                ["log_source_host"]         = "node-a",
                ["structured_data_id"]      = "acme@99999",
                ["structured_data_enabled"] = false
            });

        var resolved = SyslogSinkRuntimeConfig.From(def);

        resolved.Host.Should().Be("collector.example.com");
        resolved.Port.Should().Be(6514);
        resolved.Transport.Should().Be(SyslogTransport.Tcp);
        resolved.Format.Should().Be(SyslogFormat.Rfc5424);
        resolved.Facility.Should().Be(SyslogFacility.Local3);
        resolved.AppName.Should().Be("billing-api");
        resolved.ProcessId.Should().Be("12345");
        resolved.LogSourceHost.Should().Be("node-a");
        resolved.StructuredDataId.Should().Be("acme@99999");
        resolved.StructuredDataEnabled.Should().BeFalse();
    }

    [Fact]
    public void Defaults_match_sink_ctor_defaults_when_bag_only_carries_host()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "syslog",
            Kind: "syslog",
            Properties: new Dictionary<string, object?>
            {
                ["host"] = "collector.example.com"
            });

        var resolved = SyslogSinkRuntimeConfig.From(def);

        resolved.Host.Should().Be("collector.example.com");
        resolved.Port.Should().Be(514);
        resolved.Transport.Should().Be(SyslogTransport.Udp);
        resolved.Format.Should().Be(SyslogFormat.Rfc5424);
        resolved.Facility.Should().Be(SyslogFacility.User);
        resolved.AppName.Should().BeNull();
        resolved.ProcessId.Should().BeNull();
        resolved.LogSourceHost.Should().BeNull();
        resolved.StructuredDataId.Should().Be("herald@32473");
        resolved.StructuredDataEnabled.Should().BeTrue();
    }

    // ── Legacy fallback ────────────────────────────────────────────

    [Fact]
    public void Falls_back_to_legacy_uri_host_alias_when_bag_is_null()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "syslog",
            Kind: "syslog",
            Uri:   "collector.legacy.example.com",
            Host:  "6514",
            Alias: "tcp|rfc3164");

        var resolved = SyslogSinkRuntimeConfig.From(def);

        resolved.Host.Should().Be("collector.legacy.example.com");
        resolved.Port.Should().Be(6514);
        resolved.Transport.Should().Be(SyslogTransport.Tcp);
        resolved.Format.Should().Be(SyslogFormat.Rfc3164);
    }

    [Fact]
    public void Defaults_port_to_514_when_legacy_host_is_unparseable()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "syslog",
            Kind: "syslog",
            Uri:  "collector.example.com",
            Host: "not-a-number");

        SyslogSinkRuntimeConfig.From(def).Port.Should().Be(514);
    }

    [Fact]
    public void Bag_wins_over_legacy_slots_when_both_are_set()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "syslog",
            Kind: "syslog",
            Uri:   "legacy.example.com",
            Host:  "514",
            Alias: "udp|rfc5424",
            Properties: new Dictionary<string, object?>
            {
                ["host"]      = "bag.example.com",
                ["port"]      = 6514,
                ["transport"] = "tcp",
                ["format"]    = "rfc3164"
            });

        var resolved = SyslogSinkRuntimeConfig.From(def);

        resolved.Host.Should().Be("bag.example.com");
        resolved.Port.Should().Be(6514);
        resolved.Transport.Should().Be(SyslogTransport.Tcp);
        resolved.Format.Should().Be(SyslogFormat.Rfc3164);
    }

    // ── Facility vocabulary ────────────────────────────────────────

    [Theory]
    [InlineData("user",   SyslogFacility.User)]
    [InlineData("USER",   SyslogFacility.User)]
    [InlineData("daemon", SyslogFacility.Daemon)]
    [InlineData("auth",   SyslogFacility.Auth)]
    [InlineData("local0", SyslogFacility.Local0)]
    [InlineData("local7", SyslogFacility.Local7)]
    [InlineData("bogus",  SyslogFacility.User)]
    [InlineData("",       SyslogFacility.User)]
    public void Parses_facility_vocabulary_case_insensitively(string raw, SyslogFacility expected)
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "syslog", Kind: "syslog",
            Properties: new Dictionary<string, object?>
            {
                ["host"]     = "x",
                ["facility"] = raw
            });
        SyslogSinkRuntimeConfig.From(def).Facility.Should().Be(expected);
    }

    // ── Provider end-to-end ────────────────────────────────────────

    [Fact]
    public void Provider_creates_sink_from_bag_only_definition()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "syslog",
            Kind: "syslog",
            Properties: new Dictionary<string, object?>
            {
                ["host"] = "collector.example.com"
            });

        var sink = new SyslogSinkProvider().CreateSink(def, null!, null!);
        sink.Should().BeOfType<SyslogSink>();
    }

    [Fact]
    public void Provider_creates_sink_from_legacy_only_definition()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "syslog",
            Kind: "syslog",
            Uri:   "collector.legacy.example.com",
            Host:  "514",
            Alias: "udp|rfc5424");

        var sink = new SyslogSinkProvider().CreateSink(def, null!, null!);
        sink.Should().BeOfType<SyslogSink>();
    }

    [Fact]
    public void Provider_throws_when_host_is_missing()
    {
        var def = new LoggingRuntimeSinkDefinition(Name: "syslog", Kind: "syslog");
        var act = () => new SyslogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*host*");
    }

    // ── BLOCKER fix: STRUCTURED-DATA emission ───────────────────────

    [Fact]
    public void Rfc5424_emits_sd_element_when_event_carries_properties()
    {
        // The BLOCKER Richard called out: prior versions hardcoded "-"
        // for STRUCTURED-DATA so every Herald property dropped on the
        // wire. With the new path enabled (the default), properties
        // serialize into a single SD-ELEMENT keyed by the default
        // SD-ID.
        var evt = LogEventBuilder.Create()
            .WithTime(FixedTime)
            .WithLevel(KnownLogLevels.Info)
            .WithMessage("payment posted")
            .WithProperty("tenant", "acme")
            .WithProperty("amount", 4200)
            .Build();

        var line = SyslogMessageBuilder.Build(
            evt, SyslogFormat.Rfc5424, SyslogFacility.User,
            host: "h", appName: "billing", processId: "1",
            structuredDataId: "herald@32473", structuredDataEnabled: true);

        line.Should().Contain("[herald@32473 ");
        line.Should().Contain("tenant=\"acme\"");
        line.Should().Contain("amount=\"4200\"");
        line.Should().EndWith("payment posted");
    }

    [Fact]
    public void Rfc5424_keeps_nilvalue_sd_when_event_has_no_properties()
    {
        var evt = LogEventBuilder.Create()
            .WithTime(FixedTime)
            .WithMessage("plain")
            .Build();

        var line = SyslogMessageBuilder.Build(
            evt, SyslogFormat.Rfc5424, SyslogFacility.User,
            host: "h", appName: "a", processId: "1",
            structuredDataId: "herald@32473", structuredDataEnabled: true);

        // "h a 1 - - plain" — MSGID and STRUCTURED-DATA both NILVALUE
        // because the event has no properties to carry.
        line.Should().EndWith("h a 1 - - plain");
    }

    [Fact]
    public void Rfc5424_keeps_nilvalue_sd_when_emission_is_disabled()
    {
        // Operators on collectors that reject SD frames can flip the
        // toggle off and get the prior "drop everything" behaviour.
        var evt = LogEventBuilder.Create()
            .WithTime(FixedTime)
            .WithMessage("plain")
            .WithProperty("tenant", "acme")
            .Build();

        var line = SyslogMessageBuilder.Build(
            evt, SyslogFormat.Rfc5424, SyslogFacility.User,
            host: "h", appName: "a", processId: "1",
            structuredDataId: "herald@32473", structuredDataEnabled: false);

        line.Should().EndWith("h a 1 - - plain");
        line.Should().NotContain("[herald@32473");
    }

    [Fact]
    public void Rfc5424_escapes_quotes_backslash_and_bracket_in_property_values()
    {
        // PARAM-VALUE escaping per RFC 5424 §6.3.3: " → \", \ → \\,
        // ] → \]. A property value with all three appears once each
        // and the resulting frame still parses as a well-formed
        // SD-ELEMENT.
        var evt = LogEventBuilder.Create()
            .WithTime(FixedTime)
            .WithMessage("x")
            .WithProperty("payload", "a\"b\\c]d")
            .Build();

        var line = SyslogMessageBuilder.Build(
            evt, SyslogFormat.Rfc5424, SyslogFacility.User,
            host: "h", appName: "a", processId: "1",
            structuredDataId: "herald@32473", structuredDataEnabled: true);

        line.Should().Contain(@"payload=""a\""b\\c\]d""");
    }

    [Fact]
    public void Rfc5424_uses_operator_supplied_sd_id_in_the_element_tag()
    {
        var evt = LogEventBuilder.Create()
            .WithTime(FixedTime)
            .WithMessage("x")
            .WithProperty("tenant", "acme")
            .Build();

        var line = SyslogMessageBuilder.Build(
            evt, SyslogFormat.Rfc5424, SyslogFacility.User,
            host: "h", appName: "a", processId: "1",
            structuredDataId: "billing@99999", structuredDataEnabled: true);

        line.Should().Contain("[billing@99999 tenant=\"acme\"]");
    }

    [Fact]
    public void Rfc5424_falls_back_to_default_sd_id_when_supplied_value_is_empty()
    {
        var evt = LogEventBuilder.Create()
            .WithTime(FixedTime)
            .WithMessage("x")
            .WithProperty("tenant", "acme")
            .Build();

        var line = SyslogMessageBuilder.Build(
            evt, SyslogFormat.Rfc5424, SyslogFacility.User,
            host: "h", appName: "a", processId: "1",
            structuredDataId: "", structuredDataEnabled: true);

        line.Should().Contain("[herald@32473 tenant=\"acme\"]");
    }

    [Fact]
    public void Provider_end_to_end_property_lands_in_sd_block()
    {
        // The complete contract operators rely on: bag → provider →
        // sink → SyslogMessageBuilder.Build produces a frame with
        // the property in the SD block.
        var def = new LoggingRuntimeSinkDefinition(
            Name: "syslog",
            Kind: "syslog",
            Properties: new Dictionary<string, object?>
            {
                ["host"]               = "collector.example.com",
                ["structured_data_id"] = "billing@99999"
            });

        // The provider builds the sink; we can't observe the wire
        // through the sink without a real syslog collector. Instead
        // we re-run the equivalent SyslogMessageBuilder.Build call
        // with the same values the provider's Resolved record would
        // hand off, and assert the SD block lands the property.
        var resolved = SyslogSinkRuntimeConfig.From(def);
        resolved.StructuredDataId.Should().Be("billing@99999");
        resolved.StructuredDataEnabled.Should().BeTrue();

        var evt = LogEventBuilder.Create()
            .WithTime(FixedTime)
            .WithMessage("hello")
            .WithProperty("tenant", "acme")
            .Build();

        var line = SyslogMessageBuilder.Build(
            evt, resolved.Format, resolved.Facility,
            host: resolved.Host!,
            appName: resolved.AppName ?? "herald",
            processId: resolved.ProcessId ?? "1",
            structuredDataId: resolved.StructuredDataId,
            structuredDataEnabled: resolved.StructuredDataEnabled);

        line.Should().Contain("[billing@99999 tenant=\"acme\"]");
        line.Should().EndWith("hello");

        // The sink itself still constructs cleanly — proves the
        // provider's CreateSink path doesn't choke on this bag shape.
        new SyslogSinkProvider().CreateSink(def, null!, null!).Should().BeOfType<SyslogSink>();
    }
}
