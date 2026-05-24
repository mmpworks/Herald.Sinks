// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Herald.Sinks.PagerDuty;
using Herald.Sinks.PagerDuty.Providers;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Levels;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.PagerDuty.Tests;

/// <summary>
/// Translation-faithfulness specs for
/// <see cref="PagerDutySinkRuntimeConfig"/> plus payload-shape guards
/// for the new dedup_strategy and custom_details_template emission.
/// PagerDutyLogSink does no network I/O at construction, so the
/// provider's full CreateSink path runs through these tests.
/// </summary>
public sealed class PagerDutySinkRuntimeConfigTests
{
    // ── Bag → Resolved ──────────────────────────────────────────────

    [Fact]
    public void Reads_all_seven_keys_from_property_bag_when_present()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "pd",
            Kind: "pagerduty",
            Properties: new Dictionary<string, object?>
            {
                ["routing_key"]             = "R0123",
                ["source"]                  = "node-a",
                ["component"]               = "billing",
                ["group"]                   = "prod",
                ["endpoint"]                = "https://events.eu.pagerduty.com/v2/enqueue",
                ["dedup_strategy"]          = "category",
                ["custom_details_template"] = "team=billing, runbook_url=https://runbooks/billing"
            });

        var resolved = PagerDutySinkRuntimeConfig.From(def);

        resolved.RoutingKey.Should().Be("R0123");
        resolved.Source.Should().Be("node-a");
        resolved.Component.Should().Be("billing");
        resolved.Group.Should().Be("prod");
        resolved.Endpoint.Should().Be("https://events.eu.pagerduty.com/v2/enqueue");
        resolved.DedupStrategy.Should().Be(PagerDutyDedupStrategy.Category);
        resolved.CustomDetailsTemplate.Should().NotBeNull();
        resolved.CustomDetailsTemplate!["team"].Should().Be("billing");
        resolved.CustomDetailsTemplate["runbook_url"].Should().Be("https://runbooks/billing");
    }

    [Fact]
    public void Bag_wins_over_legacy_alias_host_uri()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name:  "pd",
            Kind:  "pagerduty",
            Alias: "legacy-routing",
            Host:  "legacy-source",
            Uri:   "https://legacy.endpoint/",
            Properties: new Dictionary<string, object?>
            {
                ["routing_key"] = "bag-routing",
                ["source"]      = "bag-source",
                ["endpoint"]    = "https://bag.endpoint/"
            });

        var resolved = PagerDutySinkRuntimeConfig.From(def);
        resolved.RoutingKey.Should().Be("bag-routing");
        resolved.Source.Should().Be("bag-source");
        resolved.Endpoint.Should().Be("https://bag.endpoint/");
    }

    [Fact]
    public void Falls_back_to_legacy_slots_when_bag_is_null()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name:  "pd",
            Kind:  "pagerduty",
            Alias: "legacy-routing",
            Host:  "legacy-source",
            Uri:   "https://legacy.endpoint/");

        var resolved = PagerDutySinkRuntimeConfig.From(def);
        resolved.RoutingKey.Should().Be("legacy-routing");
        resolved.Source.Should().Be("legacy-source");
        resolved.Endpoint.Should().Be("https://legacy.endpoint/");
    }

    [Theory]
    [InlineData("auto",     PagerDutyDedupStrategy.Auto)]
    [InlineData("AUTO",     PagerDutyDedupStrategy.Auto)]
    [InlineData("event_id", PagerDutyDedupStrategy.EventId)]
    [InlineData("template", PagerDutyDedupStrategy.Template)]
    [InlineData("category", PagerDutyDedupStrategy.Category)]
    [InlineData("message",  PagerDutyDedupStrategy.Message)]
    [InlineData("",         PagerDutyDedupStrategy.Auto)]
    [InlineData("bogus",    PagerDutyDedupStrategy.Auto)]
    public void Parses_dedup_strategy_vocabulary(string raw, PagerDutyDedupStrategy expected)
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "pd", Kind: "pagerduty",
            Properties: new Dictionary<string, object?>
            {
                ["routing_key"]    = "r",
                ["dedup_strategy"] = raw
            });
        PagerDutySinkRuntimeConfig.From(def).DedupStrategy.Should().Be(expected);
    }

    [Fact]
    public void Tolerates_blank_pairs_in_custom_details_template()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "pd", Kind: "pagerduty",
            Properties: new Dictionary<string, object?>
            {
                ["routing_key"]             = "r",
                ["custom_details_template"] = "team=billing, , no_equals_here, =empty_key, ops=true"
            });

        var resolved = PagerDutySinkRuntimeConfig.From(def);
        resolved.CustomDetailsTemplate.Should().HaveCount(2);
        resolved.CustomDetailsTemplate!["team"].Should().Be("billing");
        resolved.CustomDetailsTemplate["ops"].Should().Be("true");
    }

    // ── Provider end-to-end ─────────────────────────────────────────

    [Fact]
    public void Provider_creates_sink_from_bag_definition()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "pd", Kind: "pagerduty",
            Properties: new Dictionary<string, object?>
            {
                ["routing_key"] = "r"
            });
        var sink = new PagerDutyLogSinkProvider().CreateSink(def, null!, null!);
        sink.Should().BeOfType<PagerDutyLogSink>();
    }

    [Fact]
    public void Provider_throws_when_routing_key_is_missing()
    {
        var def = new LoggingRuntimeSinkDefinition(Name: "pd", Kind: "pagerduty");
        var act = () => new PagerDutyLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*routing_key*");
    }

    // ── Payload emission: dedup_key strategy ────────────────────────

    [Fact]
    public void Dedup_strategy_category_yields_per_category_key()
    {
        // TestHttpMessageHandler only captures the last body; for the
        // "same category → same key" property we send one event and
        // check the literal expected value. The dedup-key derivation
        // is deterministic for a given category so one observation is
        // enough to prove it.
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new PagerDutyLogSink(
            "r",
            httpClient: client,
            dedupStrategy: PagerDutyDedupStrategy.Category);

        sink.Log(LogEventBuilder.Create().WithCategory(new MMP.Herald.Events.LogCategory("auth")).Build());

        var key = JsonDocument.Parse(handler.LastRequestBodyString!)
            .RootElement.GetProperty("dedup_key").GetString();
        key.Should().Be("herald-category-auth");

        // And again with a different category — proves the value
        // actually varies with category rather than being a constant.
        sink.Log(LogEventBuilder.Create().WithCategory(new MMP.Herald.Events.LogCategory("billing")).Build());
        var key2 = JsonDocument.Parse(handler.LastRequestBodyString!)
            .RootElement.GetProperty("dedup_key").GetString();
        key2.Should().Be("herald-category-billing");
    }

    [Fact]
    public void Dedup_strategy_template_yields_per_template_key()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new PagerDutyLogSink(
            "r",
            httpClient: client,
            dedupStrategy: PagerDutyDedupStrategy.Template);

        sink.Log(LogEventBuilder.Create()
            .WithMessage("Sink {Sink} failed", "Sink DDog failed").Build());

        var key = JsonDocument.Parse(handler.LastRequestBodyString!)
            .RootElement.GetProperty("dedup_key").GetString();
        key.Should().StartWith("herald-template-");
    }

    [Fact]
    public void Dedup_strategy_auto_preserves_prior_fallback_chain()
    {
        // No event id, no template → falls back to message hash.
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new PagerDutyLogSink(
            "r",
            httpClient: client,
            dedupStrategy: PagerDutyDedupStrategy.Auto);

        sink.Log(LogEventBuilder.Create().WithMessage("plain", "plain").Build());

        var key = JsonDocument.Parse(handler.LastRequestBodyString!)
            .RootElement.GetProperty("dedup_key").GetString();
        // No EventId on the LogEventBuilder default; template is non-empty
        // string ("plain"), so auto lands at template-hash.
        key.Should().StartWith("herald-template-");
    }

    // ── Payload emission: custom_details_template ───────────────────

    [Fact]
    public void Custom_details_template_fields_appear_in_payload()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        var template = new Dictionary<string, string>
        {
            ["team"]        = "billing",
            ["runbook_url"] = "https://runbooks/billing"
        };
        using var sink = new PagerDutyLogSink(
            "r",
            httpClient: client,
            customDetailsTemplate: template);

        sink.Log(LogEventBuilder.Create().Build());

        var details = JsonDocument.Parse(handler.LastRequestBodyString!)
            .RootElement.GetProperty("payload").GetProperty("custom_details");
        details.GetProperty("team").GetString().Should().Be("billing");
        details.GetProperty("runbook_url").GetString().Should().Be("https://runbooks/billing");
    }

    [Fact]
    public void Event_property_wins_over_template_field_on_key_collision()
    {
        // Per-event data wins so a more-specific value isn't masked by
        // a static template. The sink's documentation calls this out.
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        var template = new Dictionary<string, string>
        {
            ["runbook_url"] = "https://default-runbook"
        };
        using var sink = new PagerDutyLogSink(
            "r",
            httpClient: client,
            customDetailsTemplate: template);

        sink.Log(LogEventBuilder.Create()
            .WithProperty("runbook_url", "https://event-specific-runbook")
            .Build());

        var url = JsonDocument.Parse(handler.LastRequestBodyString!)
            .RootElement.GetProperty("payload").GetProperty("custom_details")
            .GetProperty("runbook_url").GetString();
        url.Should().Be("https://event-specific-runbook");
    }
}
