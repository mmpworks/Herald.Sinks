// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using FluentAssertions;
using Herald.Sinks.Email.Providers;
using MMP.Herald.Configuration.Runtime;
using Xunit;

namespace Herald.Sinks.Email.Tests;

/// <summary>
/// Translation-faithfulness specs for
/// <see cref="EmailSinkRuntimeConfig"/>. EmailLogSink ctor does no
/// network I/O — recipient parsing and MailboxAddress validation
/// happen at construction, but the SMTP connection is per-send. So
/// the smoke tests can run the full provider path through CreateSink
/// with a valid recipient list.
/// </summary>
public sealed class EmailSinkRuntimeConfigTests
{
    // ── Bag → Resolved ──────────────────────────────────────────────

    [Fact]
    public void Reads_all_eight_keys_from_property_bag_when_present()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "email",
            Kind: "email",
            Properties: new Dictionary<string, object?>
            {
                ["smtp_host"]        = "smtp.example.com",
                ["smtp_port"]        = 465,
                ["from_address"]     = "alerts@example.com",
                ["to_addresses"]     = "oncall@example.com, ops@example.com",
                ["username"]         = "alerts@example.com",
                ["password"]         = "pw",
                ["use_start_tls"]    = false,
                ["subject_template"] = "Herald {level}"
            });

        var resolved = EmailSinkRuntimeConfig.From(def);

        resolved.SmtpHost.Should().Be("smtp.example.com");
        resolved.SmtpPort.Should().Be(465);
        resolved.FromAddress.Should().Be("alerts@example.com");
        resolved.ToAddresses.Should().BeEquivalentTo(new[] { "oncall@example.com", "ops@example.com" });
        resolved.Username.Should().Be("alerts@example.com");
        resolved.Password.Should().Be("pw");
        resolved.UseStartTls.Should().BeFalse();
        resolved.SubjectTemplate.Should().Be("Herald {level}");
    }

    [Fact]
    public void Defaults_port_subject_and_starttls_when_absent()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "email",
            Kind: "email",
            Properties: new Dictionary<string, object?>
            {
                ["smtp_host"]    = "smtp.example.com",
                ["from_address"] = "alerts@example.com",
                ["to_addresses"] = "oncall@example.com"
            });

        var resolved = EmailSinkRuntimeConfig.From(def);

        resolved.SmtpPort.Should().Be(587);
        resolved.UseStartTls.Should().BeTrue();
        resolved.SubjectTemplate.Should().Be("[Herald {level}] log alert");
    }

    [Fact]
    public void Splits_recipients_on_commas_trimming_whitespace()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "email",
            Kind: "email",
            Properties: new Dictionary<string, object?>
            {
                ["smtp_host"]    = "smtp.example.com",
                ["from_address"] = "from@example.com",
                ["to_addresses"] = "  a@x , b@x ,  c@x  "
            });

        var resolved = EmailSinkRuntimeConfig.From(def);

        resolved.ToAddresses.Should().BeEquivalentTo(new[] { "a@x", "b@x", "c@x" });
    }

    [Fact]
    public void Accepts_string_port_from_json_deserialiser()
    {
        // The dashboard's JSON sometimes carries integers as quoted
        // strings; the mapper must accept both shapes or the default
        // silently wins.
        var def = new LoggingRuntimeSinkDefinition(
            Name: "email",
            Kind: "email",
            Properties: new Dictionary<string, object?>
            {
                ["smtp_host"]    = "smtp.example.com",
                ["smtp_port"]    = "25",
                ["from_address"] = "from@example.com",
                ["to_addresses"] = "to@example.com"
            });

        var resolved = EmailSinkRuntimeConfig.From(def);

        resolved.SmtpPort.Should().Be(25);
    }

    [Fact]
    public void Bag_wins_over_legacy_slots_when_both_are_set()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "email",
            Kind: "email",
            Uri:   "smtp.legacy.example.com",
            Host:  "legacy@example.com",
            Alias: "legacy-pass",
            Properties: new Dictionary<string, object?>
            {
                ["smtp_host"]    = "smtp.bag.example.com",
                ["from_address"] = "bag@example.com",
                ["to_addresses"] = "to@example.com",
                ["password"]     = "bag-pass"
            });

        var resolved = EmailSinkRuntimeConfig.From(def);

        resolved.SmtpHost.Should().Be("smtp.bag.example.com");
        resolved.FromAddress.Should().Be("bag@example.com");
        resolved.Password.Should().Be("bag-pass");
    }

    // ── Provider end-to-end ─────────────────────────────────────────

    [Fact]
    public void Provider_creates_sink_from_bag_only_definition()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "email",
            Kind: "email",
            Properties: new Dictionary<string, object?>
            {
                ["smtp_host"]    = "smtp.example.com",
                ["from_address"] = "alerts@example.com",
                ["to_addresses"] = "oncall@example.com"
            });

        var sink = new EmailLogSinkProvider().CreateSink(def, null!, null!);
        sink.Should().NotBeNull();
        sink.Should().BeOfType<EmailLogSink>();
    }

    [Fact]
    public void Provider_throws_when_smtp_host_is_missing()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "email",
            Kind: "email",
            Properties: new Dictionary<string, object?>
            {
                ["from_address"] = "from@x",
                ["to_addresses"] = "to@x"
            });

        var act = () => new EmailLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*smtp_host*");
    }

    [Fact]
    public void Provider_throws_when_from_address_is_missing()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "email",
            Kind: "email",
            Properties: new Dictionary<string, object?>
            {
                ["smtp_host"]    = "smtp.x",
                ["to_addresses"] = "to@x"
            });

        var act = () => new EmailLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*from_address*");
    }

    [Fact]
    public void Provider_throws_when_to_addresses_is_missing_or_empty()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "email",
            Kind: "email",
            Properties: new Dictionary<string, object?>
            {
                ["smtp_host"]    = "smtp.x",
                ["from_address"] = "from@x"
            });

        var act = () => new EmailLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*to_addresses*");
    }

    [Fact]
    public void Provider_throws_when_to_addresses_parses_to_empty_list()
    {
        // A comma-only or whitespace-only string parses to no
        // recipients — provider catches this with the same message
        // as the missing-key case.
        var def = new LoggingRuntimeSinkDefinition(
            Name: "email",
            Kind: "email",
            Properties: new Dictionary<string, object?>
            {
                ["smtp_host"]    = "smtp.x",
                ["from_address"] = "from@x",
                ["to_addresses"] = " , , "
            });

        var act = () => new EmailLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*to_addresses*");
    }
}
