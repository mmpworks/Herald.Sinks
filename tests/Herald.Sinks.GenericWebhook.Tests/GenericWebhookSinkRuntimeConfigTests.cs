// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using FluentAssertions;
using Herald.Sinks.GenericWebhook;
using Herald.Sinks.GenericWebhook.Providers;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Levels;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.GenericWebhook.Tests;

/// <summary>
/// Translation-faithfulness specs for
/// <see cref="GenericWebhookSinkRuntimeConfig"/> plus header
/// emission guards proving the bag → provider → sink → wire pipe.
/// </summary>
public sealed class GenericWebhookSinkRuntimeConfigTests
{
    private readonly ILogLevelRegistry _registry = RegistryHelper.CreateDefault();

    // ── Bag → Resolved ──────────────────────────────────────────────

    [Fact]
    public void Reads_all_three_keys_from_property_bag_when_present()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "wh",
            Kind: "webhook",
            Properties: new Dictionary<string, object?>
            {
                ["url"]          = "https://hooks.example.com/incoming",
                ["headers"]      = "Authorization=Bearer abc, X-Tenant=acme",
                ["content_type"] = "application/x-ndjson"
            });

        var resolved = GenericWebhookSinkRuntimeConfig.From(def);

        resolved.Url.Should().Be("https://hooks.example.com/incoming");
        resolved.Headers.Should().NotBeNull();
        resolved.Headers!["Authorization"].Should().Be("Bearer abc");
        resolved.Headers["X-Tenant"].Should().Be("acme");
        resolved.ContentType.Should().Be("application/x-ndjson");
    }

    [Fact]
    public void Defaults_content_type_to_application_json_when_absent()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "wh", Kind: "webhook",
            Properties: new Dictionary<string, object?>
            {
                ["url"] = "https://hooks.example.com"
            });
        GenericWebhookSinkRuntimeConfig.From(def).ContentType.Should().Be("application/json");
    }

    [Fact]
    public void Falls_back_to_legacy_uri_for_url()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "wh", Kind: "webhook",
            Uri: "https://legacy.example.com");
        GenericWebhookSinkRuntimeConfig.From(def).Url.Should().Be("https://legacy.example.com");
    }

    [Fact]
    public void Bag_url_wins_over_legacy_uri()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "wh", Kind: "webhook",
            Uri: "https://legacy.example.com",
            Properties: new Dictionary<string, object?>
            {
                ["url"] = "https://bag.example.com"
            });
        GenericWebhookSinkRuntimeConfig.From(def).Url.Should().Be("https://bag.example.com");
    }

    [Fact]
    public void Tolerates_blank_pairs_in_headers()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "wh", Kind: "webhook",
            Properties: new Dictionary<string, object?>
            {
                ["url"]     = "https://x",
                ["headers"] = "Authorization=Bearer abc, , malformed_no_equals, =no_key, X-Tenant=acme"
            });

        var resolved = GenericWebhookSinkRuntimeConfig.From(def);
        resolved.Headers.Should().HaveCount(2);
        resolved.Headers!["Authorization"].Should().Be("Bearer abc");
        resolved.Headers["X-Tenant"].Should().Be("acme");
    }

    // ── Provider end-to-end ─────────────────────────────────────────

    [Fact]
    public void Provider_throws_when_url_is_missing()
    {
        var def = new LoggingRuntimeSinkDefinition(Name: "wh", Kind: "webhook");
        var act = () => new GenericWebhookSinkProvider().CreateSink(def, _registry, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*url*");
    }

    [Fact]
    public void Sink_emits_headers_on_outgoing_request_when_bag_supplies_them()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new GenericWebhookLogSink(
            url: "http://wh/in",
            levelRegistry: _registry,
            headers: new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer abc",
                ["X-Tenant"]      = "acme"
            },
            httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        var req = handler.Requests[0];
        req.Headers.Authorization.Should().NotBeNull();
        req.Headers.Authorization!.Scheme.Should().Be("Bearer");
        req.Headers.Authorization.Parameter.Should().Be("abc");
        req.Headers.GetValues("X-Tenant").Should().ContainSingle().Which.Should().Be("acme");
    }

    [Fact]
    public void Sink_emits_no_auth_when_headers_dictionary_is_null()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new GenericWebhookLogSink(
            url: "http://wh/in",
            levelRegistry: _registry,
            httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        handler.Requests[0].Headers.Authorization.Should().BeNull();
    }
}
