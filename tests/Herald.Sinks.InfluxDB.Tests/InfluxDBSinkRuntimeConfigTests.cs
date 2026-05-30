// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using FluentAssertions;
using Herald.Sinks.InfluxDB.Providers;
using MMP.Herald.Configuration.Runtime;
using Xunit;

namespace Herald.Sinks.InfluxDB.Tests;

/// <summary>
/// Translation-faithfulness specs for
/// <see cref="InfluxDBSinkRuntimeConfig"/>. The sink constructor
/// does no network I/O (it only builds a Uri and lazily holds an
/// HttpClient), so the smoke tests can run the full provider path
/// through CreateSink.
/// </summary>
public sealed class InfluxDBSinkRuntimeConfigTests
{
    // ── Bag → Resolved ──────────────────────────────────────────────

    [Fact]
    public void Reads_all_four_keys_from_property_bag_when_present()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "influx",
            Kind: "influxdb",
            Properties: new Dictionary<string, object?>
            {
                ["server_url"]   = "https://influx.example.com",
                ["organization"] = "acme",
                ["bucket"]       = "logs",
                ["token"]        = "tok"
            });

        var resolved = InfluxDBSinkRuntimeConfig.From(def);

        resolved.ServerUrl.Should().Be("https://influx.example.com");
        resolved.Organization.Should().Be("acme");
        resolved.Bucket.Should().Be("logs");
        resolved.Token.Should().Be("tok");
    }

    [Fact]
    public void Bag_wins_over_legacy_slots_when_both_are_set()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "influx",
            Kind: "influxdb",
            Uri:   "https://legacy.example.com",
            Host:  "legacy-org",
            Alias: "legacy-token",
            Properties: new Dictionary<string, object?>
            {
                ["server_url"]   = "https://bag.example.com",
                ["organization"] = "bag-org",
                ["bucket"]       = "bag-bucket",
                ["token"]        = "bag-token"
            });

        var resolved = InfluxDBSinkRuntimeConfig.From(def);

        resolved.ServerUrl.Should().Be("https://bag.example.com");
        resolved.Organization.Should().Be("bag-org");
        resolved.Bucket.Should().Be("bag-bucket");
        resolved.Token.Should().Be("bag-token");
    }

    [Fact]
    public void Falls_back_to_legacy_slots_for_three_of_four_fields()
    {
        // bucket has no legacy mapping.
        var def = new LoggingRuntimeSinkDefinition(
            Name: "influx",
            Kind: "influxdb",
            Uri:   "https://legacy.example.com",
            Host:  "legacy-org",
            Alias: "legacy-token",
            Properties: new Dictionary<string, object?>
            {
                ["bucket"] = "bag-bucket"
            });

        var resolved = InfluxDBSinkRuntimeConfig.From(def);

        resolved.ServerUrl.Should().Be("https://legacy.example.com");
        resolved.Organization.Should().Be("legacy-org");
        resolved.Token.Should().Be("legacy-token");
        resolved.Bucket.Should().Be("bag-bucket");
    }

    // ── Provider end-to-end ─────────────────────────────────────────

    [Fact]
    public void Provider_creates_sink_from_bag_only_definition()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "influx",
            Kind: "influxdb",
            Properties: new Dictionary<string, object?>
            {
                ["server_url"]   = "https://influx.example.com",
                ["organization"] = "acme",
                ["bucket"]       = "logs",
                ["token"]        = "tok"
            });

        var sink = new InfluxDBLogSinkProvider().CreateSink(def, null!, null!);
        sink.Should().NotBeNull();
        sink.Should().BeOfType<InfluxDBLogSink>();
    }

    [Fact]
    public void Provider_throws_when_server_url_is_missing()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "influx",
            Kind: "influxdb",
            Properties: new Dictionary<string, object?>
            {
                ["organization"] = "o",
                ["bucket"]       = "b",
                ["token"]        = "t"
            });

        var act = () => new InfluxDBLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*server_url*");
    }

    [Fact]
    public void Provider_throws_when_organization_is_missing()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "influx",
            Kind: "influxdb",
            Properties: new Dictionary<string, object?>
            {
                ["server_url"] = "https://x",
                ["bucket"]     = "b",
                ["token"]      = "t"
            });

        var act = () => new InfluxDBLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*organization*");
    }

    [Fact]
    public void Provider_throws_when_bucket_is_missing()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "influx",
            Kind: "influxdb",
            Properties: new Dictionary<string, object?>
            {
                ["server_url"]   = "https://x",
                ["organization"] = "o",
                ["token"]        = "t"
            });

        var act = () => new InfluxDBLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*bucket*");
    }

    [Fact]
    public void Provider_throws_when_token_is_missing()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "influx",
            Kind: "influxdb",
            Properties: new Dictionary<string, object?>
            {
                ["server_url"]   = "https://x",
                ["organization"] = "o",
                ["bucket"]       = "b"
            });

        var act = () => new InfluxDBLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*token*");
    }
}
