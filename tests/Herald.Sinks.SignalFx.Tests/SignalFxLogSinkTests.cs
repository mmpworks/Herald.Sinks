// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Herald.Sinks.SignalFx;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Services;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.SignalFx.Tests;

public sealed class SignalFxLogSinkTests
{
    private const string AccessToken = "sfx-token-1234567890abcdef";

    [Fact]
    public void Log_posts_json_array_with_core_fields()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SignalFxLogSink(AccessToken, realm: "us1", httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Warn)
            .WithMessage("Template with {x}", "Template with 42")
            .Build();

        sink.Log(evt);

        handler.RequestCount.Should().Be(1);
        var doc = JsonDocument.Parse(handler.LastRequestBodyString!);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        var entry = doc.RootElement[0];
        entry.GetProperty("timestamp").GetInt64().Should().BeGreaterThan(0);
        entry.GetProperty("message").GetString().Should().Be("Template with 42");
        entry.GetProperty("level").GetString().Should().Be("warn");
    }

    [Fact]
    public void Log_sets_x_sf_token_header()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SignalFxLogSink(AccessToken, realm: "us1", httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        handler.Requests[0].Headers.GetValues("X-SF-Token").Should().ContainSingle().Which.Should().Be(AccessToken);
    }

    [Fact]
    public void Realm_builds_default_ingest_url_with_v2_log_path()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SignalFxLogSink(AccessToken, realm: "us1", httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        handler.Requests[0].RequestUri!.ToString()
            .Should().Be("https://ingest.us1.signalfx.com/v2/log");
    }

    [Fact]
    public void Endpoint_override_wins_over_realm()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SignalFxLogSink(
            AccessToken,
            endpoint: "https://signalfx-onprem.example.com",
            realm: "ignored",
            httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        handler.Requests[0].RequestUri!.ToString()
            .Should().Be("https://signalfx-onprem.example.com/v2/log");
    }

    [Fact]
    public void Dimensions_appear_in_payload_when_supplied()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        var dims = new Dictionary<string, string> { ["env"] = "prod", ["service"] = "api" };
        using var sink = new SignalFxLogSink(AccessToken, realm: "us1", dimensions: dims, httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        var entry = JsonDocument.Parse(handler.LastRequestBodyString!).RootElement[0];
        var dim = entry.GetProperty("dimensions");
        dim.GetProperty("env").GetString().Should().Be("prod");
        dim.GetProperty("service").GetString().Should().Be("api");
    }

    [Fact]
    public void Properties_land_at_payload_root_alongside_reserved_fields()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SignalFxLogSink(AccessToken, realm: "us1", httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithProperty("UserId", 7)
            .WithProperty("Region", "us-west-2")
            .Build();

        sink.Log(evt);

        var entry = JsonDocument.Parse(handler.LastRequestBodyString!).RootElement[0];
        entry.GetProperty("UserId").GetInt32().Should().Be(7);
        entry.GetProperty("Region").GetString().Should().Be("us-west-2");
    }

    [Fact]
    public void LogBatch_empty_is_a_no_op()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SignalFxLogSink(AccessToken, realm: "us1", httpClient: client);

        sink.LogBatch(Array.Empty<LogEvent>());

        handler.RequestCount.Should().Be(0);
    }
}
