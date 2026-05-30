// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using FluentAssertions;
using Herald.Sinks.Twilio.Providers;
using MMP.Herald.Configuration.Runtime;
using Xunit;

namespace Herald.Sinks.Twilio.Tests;

/// <summary>
/// Translation-faithfulness specs for
/// <see cref="TwilioSinkRuntimeConfig"/>. The sink constructor does
/// no network I/O, so smoke tests cover the full provider path
/// through CreateSink.
/// </summary>
public sealed class TwilioSinkRuntimeConfigTests
{
    // ── Bag → Resolved ──────────────────────────────────────────────

    [Fact]
    public void Reads_all_four_keys_from_property_bag_when_present()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "twilio",
            Kind: "twilio",
            Properties: new Dictionary<string, object?>
            {
                ["account_sid"] = "AC123",
                ["auth_token"]  = "tok",
                ["from_number"] = "+15551234567",
                ["to_number"]   = "+15559876543"
            });

        var resolved = TwilioSinkRuntimeConfig.From(def);

        resolved.AccountSid.Should().Be("AC123");
        resolved.AuthToken.Should().Be("tok");
        resolved.FromNumber.Should().Be("+15551234567");
        resolved.ToNumber.Should().Be("+15559876543");
    }

    [Fact]
    public void Bag_wins_over_legacy_slots_when_both_are_set()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "twilio",
            Kind: "twilio",
            Uri:   "AClegacy",
            Alias: "legacy-token",
            Host:  "+1555000",
            Properties: new Dictionary<string, object?>
            {
                ["account_sid"] = "ACbag",
                ["auth_token"]  = "bag-token",
                ["from_number"] = "+1555111",
                ["to_number"]   = "+1555222"
            });

        var resolved = TwilioSinkRuntimeConfig.From(def);

        resolved.AccountSid.Should().Be("ACbag");
        resolved.AuthToken.Should().Be("bag-token");
        resolved.FromNumber.Should().Be("+1555111");
        resolved.ToNumber.Should().Be("+1555222");
    }

    [Fact]
    public void Falls_back_to_legacy_slots_for_three_of_four_fields()
    {
        // to_number has no legacy mapping.
        var def = new LoggingRuntimeSinkDefinition(
            Name: "twilio",
            Kind: "twilio",
            Uri:   "AClegacy",
            Alias: "legacy-token",
            Host:  "+1555000",
            Properties: new Dictionary<string, object?>
            {
                ["to_number"] = "+1555222"
            });

        var resolved = TwilioSinkRuntimeConfig.From(def);

        resolved.AccountSid.Should().Be("AClegacy");
        resolved.AuthToken.Should().Be("legacy-token");
        resolved.FromNumber.Should().Be("+1555000");
        resolved.ToNumber.Should().Be("+1555222");
    }

    // ── Provider end-to-end ─────────────────────────────────────────

    [Fact]
    public void Provider_creates_sink_from_bag_only_definition()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "twilio",
            Kind: "twilio",
            Properties: new Dictionary<string, object?>
            {
                ["account_sid"] = "AC123",
                ["auth_token"]  = "tok",
                ["from_number"] = "+15551234567",
                ["to_number"]   = "+15559876543"
            });

        var sink = new TwilioLogSinkProvider().CreateSink(def, null!, null!);
        sink.Should().NotBeNull();
        sink.Should().BeOfType<TwilioLogSink>();
    }

    [Fact]
    public void Provider_throws_when_account_sid_is_missing()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "twilio",
            Kind: "twilio",
            Properties: new Dictionary<string, object?>
            {
                ["auth_token"]  = "t",
                ["from_number"] = "+1",
                ["to_number"]   = "+2"
            });

        var act = () => new TwilioLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*account_sid*");
    }

    [Fact]
    public void Provider_throws_when_auth_token_is_missing()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "twilio",
            Kind: "twilio",
            Properties: new Dictionary<string, object?>
            {
                ["account_sid"] = "AC",
                ["from_number"] = "+1",
                ["to_number"]   = "+2"
            });

        var act = () => new TwilioLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*auth_token*");
    }

    [Fact]
    public void Provider_throws_when_from_number_is_missing()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "twilio",
            Kind: "twilio",
            Properties: new Dictionary<string, object?>
            {
                ["account_sid"] = "AC",
                ["auth_token"]  = "t",
                ["to_number"]   = "+2"
            });

        var act = () => new TwilioLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*from_number*");
    }

    [Fact]
    public void Provider_throws_when_to_number_is_missing()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "twilio",
            Kind: "twilio",
            Properties: new Dictionary<string, object?>
            {
                ["account_sid"] = "AC",
                ["auth_token"]  = "t",
                ["from_number"] = "+1"
            });

        var act = () => new TwilioLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*to_number*");
    }
}
