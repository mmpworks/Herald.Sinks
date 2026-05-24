// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System.Collections.Generic;
using FluentAssertions;
using Herald.Sinks.Mqtt.Providers;
using MMP.Herald.Configuration.Runtime;
using Xunit;

namespace Herald.Sinks.Mqtt.Tests;

/// <summary>
/// Translation-faithfulness specs for
/// <see cref="MqttSinkRuntimeConfig"/>. Tests stop at the typed
/// Resolved record because the production <see cref="MqttLogSink"/>
/// constructor opens a real TCP connection — outside the scope of
/// a smoke test. End-to-end provider construction is covered by
/// integration tests that own broker provisioning.
/// </summary>
public sealed class MqttSinkRuntimeConfigTests
{
    // ── Bag → Resolved ──────────────────────────────────────────────

    [Fact]
    public void Reads_broker_and_topic_from_property_bag_when_present()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "mqtt",
            Kind: "mqtt",
            Properties: new Dictionary<string, object?>
            {
                ["broker"] = "broker.example.com:8883",
                ["topic"]  = "herald/logs/audit"
            });

        var resolved = MqttSinkRuntimeConfig.From(def);

        resolved.BrokerHost.Should().Be("broker.example.com");
        resolved.BrokerPort.Should().Be(8883);
        resolved.Topic.Should().Be("herald/logs/audit");
    }

    [Fact]
    public void Defaults_port_to_1883_when_broker_has_no_port_segment()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "mqtt",
            Kind: "mqtt",
            Properties: new Dictionary<string, object?>
            {
                ["broker"] = "broker.example.com"
            });

        var resolved = MqttSinkRuntimeConfig.From(def);

        resolved.BrokerHost.Should().Be("broker.example.com");
        resolved.BrokerPort.Should().Be(1883);
    }

    [Fact]
    public void Defaults_port_to_1883_when_broker_port_is_unparseable()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "mqtt",
            Kind: "mqtt",
            Properties: new Dictionary<string, object?>
            {
                ["broker"] = "broker.example.com:not-a-port"
            });

        var resolved = MqttSinkRuntimeConfig.From(def);

        resolved.BrokerHost.Should().Be("broker.example.com");
        resolved.BrokerPort.Should().Be(1883);
    }

    [Fact]
    public void Defaults_topic_to_herald_logs_when_neither_source_supplies_one()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "mqtt",
            Kind: "mqtt",
            Properties: new Dictionary<string, object?>
            {
                ["broker"] = "broker.example.com"
            });

        var resolved = MqttSinkRuntimeConfig.From(def);

        resolved.Topic.Should().Be("herald/logs");
    }

    [Fact]
    public void Bag_wins_over_legacy_slots_when_both_are_set()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "mqtt",
            Kind: "mqtt",
            Uri:   "legacy.example.com:9000",
            Host:  "legacy/topic",
            Properties: new Dictionary<string, object?>
            {
                ["broker"] = "bag.example.com:8883",
                ["topic"]  = "bag/topic"
            });

        var resolved = MqttSinkRuntimeConfig.From(def);

        resolved.BrokerHost.Should().Be("bag.example.com");
        resolved.BrokerPort.Should().Be(8883);
        resolved.Topic.Should().Be("bag/topic");
    }

    [Fact]
    public void Falls_back_to_legacy_slots_when_bag_is_null()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "mqtt",
            Kind: "mqtt",
            Uri:   "legacy.example.com:9000",
            Host:  "legacy/topic");

        var resolved = MqttSinkRuntimeConfig.From(def);

        resolved.BrokerHost.Should().Be("legacy.example.com");
        resolved.BrokerPort.Should().Be(9000);
        resolved.Topic.Should().Be("legacy/topic");
    }

    [Fact]
    public void Falls_back_to_legacy_when_bag_carries_empty_values()
    {
        // Empty bag values match a form field the operator left blank —
        // they should not stomp the matching legacy slot.
        var def = new LoggingRuntimeSinkDefinition(
            Name: "mqtt",
            Kind: "mqtt",
            Uri:   "legacy.example.com:9000",
            Host:  "legacy/topic",
            Properties: new Dictionary<string, object?>
            {
                ["broker"] = "",
                ["topic"]  = ""
            });

        var resolved = MqttSinkRuntimeConfig.From(def);

        resolved.BrokerHost.Should().Be("legacy.example.com");
        resolved.BrokerPort.Should().Be(9000);
        resolved.Topic.Should().Be("legacy/topic");
    }

    [Fact]
    public void Returns_null_broker_when_neither_source_supplies_one()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "mqtt",
            Kind: "mqtt");

        var resolved = MqttSinkRuntimeConfig.From(def);

        resolved.BrokerHost.Should().BeNull();
        resolved.BrokerPort.Should().Be(1883);
        resolved.Topic.Should().Be("herald/logs");
    }
}
