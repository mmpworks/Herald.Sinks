// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using FluentAssertions;
using Herald.Sinks.Kafka.Providers;
using MMP.Herald.Configuration.Runtime;
using Xunit;

namespace Herald.Sinks.Kafka.Tests;

/// <summary>
/// Translation-faithfulness specs for
/// <see cref="KafkaSinkRuntimeConfig"/> plus the provider's
/// required-field and SASL-completeness guards. Full sink
/// construction is not exercised here because the connection-string
/// ctor eagerly builds an IProducer that contacts the broker —
/// integration territory. The provider's guards run before the
/// producer build, so they're reachable through CreateSink with no
/// network at all.
/// </summary>
public sealed class KafkaSinkRuntimeConfigTests
{
    // ── Bag → Resolved ──────────────────────────────────────────────

    [Fact]
    public void Reads_all_five_keys_from_property_bag_when_present()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "kafka",
            Kind: "kafka",
            Properties: new Dictionary<string, object?>
            {
                ["bootstrap_servers"] = "broker1:9092,broker2:9092",
                ["topic"]             = "herald.logs",
                ["sasl_mechanism"]    = "SCRAM-SHA-512",
                ["sasl_username"]     = "logger",
                ["sasl_password"]     = "secret"
            });

        var resolved = KafkaSinkRuntimeConfig.From(def);

        resolved.BootstrapServers.Should().Be("broker1:9092,broker2:9092");
        resolved.Topic.Should().Be("herald.logs");
        resolved.SaslMechanism.Should().Be("SCRAM-SHA-512");
        resolved.SaslUsername.Should().Be("logger");
        resolved.SaslPassword.Should().Be("secret");
    }

    [Fact]
    public void Falls_back_to_legacy_slots_when_bag_is_null()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "kafka",
            Kind: "kafka",
            Uri:  "legacy:9092",
            Host: "legacy.topic");

        var resolved = KafkaSinkRuntimeConfig.From(def);

        resolved.BootstrapServers.Should().Be("legacy:9092");
        resolved.Topic.Should().Be("legacy.topic");
        resolved.SaslMechanism.Should().BeNull();
    }

    [Fact]
    public void Bag_wins_over_legacy_slots_when_both_are_set()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "kafka",
            Kind: "kafka",
            Uri:  "legacy:9092",
            Host: "legacy.topic",
            Properties: new Dictionary<string, object?>
            {
                ["bootstrap_servers"] = "bag:9092",
                ["topic"]             = "bag.topic"
            });

        var resolved = KafkaSinkRuntimeConfig.From(def);
        resolved.BootstrapServers.Should().Be("bag:9092");
        resolved.Topic.Should().Be("bag.topic");
    }

    // ── Provider guards (no broker connection) ──────────────────────

    [Fact]
    public void Provider_throws_when_bootstrap_servers_is_missing()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "kafka",
            Kind: "kafka",
            Properties: new Dictionary<string, object?>
            {
                ["topic"] = "t"
            });

        var act = () => new KafkaLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*bootstrap_servers*");
    }

    [Fact]
    public void Provider_throws_when_topic_is_missing()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "kafka",
            Kind: "kafka",
            Properties: new Dictionary<string, object?>
            {
                ["bootstrap_servers"] = "x:9092"
            });

        var act = () => new KafkaLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*topic*");
    }

    [Fact]
    public void Provider_rejects_sasl_mechanism_without_username()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "kafka",
            Kind: "kafka",
            Properties: new Dictionary<string, object?>
            {
                ["bootstrap_servers"] = "x:9092",
                ["topic"]             = "t",
                ["sasl_mechanism"]    = "PLAIN",
                ["sasl_password"]     = "p"
            });

        var act = () => new KafkaLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*sasl_username*");
    }

    [Fact]
    public void Provider_rejects_sasl_mechanism_without_password()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "kafka",
            Kind: "kafka",
            Properties: new Dictionary<string, object?>
            {
                ["bootstrap_servers"] = "x:9092",
                ["topic"]             = "t",
                ["sasl_mechanism"]    = "PLAIN",
                ["sasl_username"]     = "u"
            });

        var act = () => new KafkaLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*sasl_password*");
    }
}
