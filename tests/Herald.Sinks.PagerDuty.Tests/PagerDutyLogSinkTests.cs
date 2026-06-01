// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Herald.Sinks.PagerDuty;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Services;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.PagerDuty.Tests;

public sealed class PagerDutyLogSinkTests
{
    private const string RoutingKey = "R02TEST-routing-key";

    [Fact]
    public void Log_posts_trigger_event_with_payload()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new PagerDutyLogSink(RoutingKey, source: "node-a", httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Fatal)
            .WithMessage("Database offline", "Database offline")
            .Build();

        sink.Log(evt);

        handler.RequestCount.Should().Be(1);
        var doc = JsonDocument.Parse(handler.LastRequestBodyString!);
        doc.RootElement.GetProperty("routing_key").GetString().Should().Be(RoutingKey);
        doc.RootElement.GetProperty("event_action").GetString().Should().Be("trigger");
        doc.RootElement.GetProperty("dedup_key").GetString().Should().NotBeNullOrWhiteSpace();

        var payload = doc.RootElement.GetProperty("payload");
        payload.GetProperty("summary").GetString().Should().Be("Database offline");
        payload.GetProperty("severity").GetString().Should().Be("critical");
        payload.GetProperty("source").GetString().Should().Be("node-a");
    }

    [Fact]
    public void Endpoint_defaults_to_public_events_api()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new PagerDutyLogSink(RoutingKey, httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        handler.Requests[0].RequestUri!.ToString()
            .Should().Be("https://events.pagerduty.com/v2/enqueue");
    }

    [Theory]
    [InlineData("verbose", "info")]
    [InlineData("information", "info")]
    [InlineData("warning", "warning")]
    [InlineData("error", "error")]
    [InlineData("fatal", "critical")]
    public void Severity_maps_herald_levels_to_pagerduty_values(string heraldKey, string expected)
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new PagerDutyLogSink(RoutingKey, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithLevel(new LogLevel(heraldKey, heraldKey))
            .Build();

        sink.Log(evt);

        var severity = JsonDocument.Parse(handler.LastRequestBodyString!)
            .RootElement.GetProperty("payload").GetProperty("severity").GetString();
        severity.Should().Be(expected);
    }

    [Fact]
    public void Dedup_key_stable_for_same_template()
    {
        // Two separate sinks so each handler's LastRequestBodyString
        // captures its one Log call cleanly — the shared-handler pattern
        // overwrites the body buffer on every request, and reaching back
        // through Requests[0].Content doesn't work once the handler
        // consumed the stream for its capture.
        var template = "Sink {name} timed out";

        var handlerA = new TestHttpMessageHandler();
        using var sinkA = new PagerDutyLogSink(RoutingKey, httpClient: new HttpClient(handlerA));
        sinkA.Log(LogEventBuilder.Create().WithMessage(template, "Sink A timed out").Build());
        var first = JsonDocument.Parse(handlerA.LastRequestBodyString!)
            .RootElement.GetProperty("dedup_key").GetString();

        var handlerB = new TestHttpMessageHandler();
        using var sinkB = new PagerDutyLogSink(RoutingKey, httpClient: new HttpClient(handlerB));
        sinkB.Log(LogEventBuilder.Create().WithMessage(template, "Sink B timed out").Build());
        var second = JsonDocument.Parse(handlerB.LastRequestBodyString!)
            .RootElement.GetProperty("dedup_key").GetString();

        first.Should().Be(second, "two events with the same template collapse to one PagerDuty incident");
    }

    [Fact]
    public void Custom_dedup_resolver_overrides_default()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new PagerDutyLogSink(
            RoutingKey,
            dedupKeyResolver: evt => "custom-" + evt.Category.Value,
            httpClient: client);

        var evt = LogEventBuilder.Create().WithCategory(new LogCategory("auth")).Build();
        sink.Log(evt);

        var dedup = JsonDocument.Parse(handler.LastRequestBodyString!)
            .RootElement.GetProperty("dedup_key").GetString();
        dedup.Should().Be("custom-auth");
    }

    [Fact]
    public void Exception_lands_in_custom_details()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new PagerDutyLogSink(RoutingKey, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithContext(LogContextKeys.Exception, new InvalidOperationException("boom"))
            .Build();

        sink.Log(evt);

        var details = JsonDocument.Parse(handler.LastRequestBodyString!)
            .RootElement.GetProperty("payload").GetProperty("custom_details");
        details.GetProperty("exception").GetString().Should().Contain("boom");
        details.GetProperty("exception.type").GetString().Should().Be("System.InvalidOperationException");
    }
}
