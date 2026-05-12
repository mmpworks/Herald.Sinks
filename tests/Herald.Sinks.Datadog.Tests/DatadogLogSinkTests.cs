// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Herald.Sinks.Datadog;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Services;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Datadog.Tests;

public sealed class DatadogLogSinkTests
{
    private const string ApiKey = "dd-api-key";
    private const string Service = "herald-test-service";

    [Fact]
    public void Log_posts_json_array_with_reserved_fields()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new DatadogLogSink(ApiKey, Service, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Warn)
            .WithMessage("Low disk on {host}", "Low disk on nodeA")
            .Build();

        sink.Log(evt);

        handler.RequestCount.Should().Be(1);
        var doc = JsonDocument.Parse(handler.LastRequestBodyString!);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().Should().Be(1);

        var entry = doc.RootElement[0];
        entry.GetProperty("message").GetString().Should().Be("Low disk on nodeA");
        entry.GetProperty("ddsource").GetString().Should().Be("herald");
        entry.GetProperty("service").GetString().Should().Be(Service);
        entry.GetProperty("status").GetString().Should().Be("warn");
        entry.GetProperty("messageTemplate").GetString().Should().Be("Low disk on {host}");
    }

    [Fact]
    public void Log_sets_dd_api_key_header()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new DatadogLogSink(ApiKey, Service, httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        handler.Requests[0].Headers.GetValues("DD-API-KEY").Should().ContainSingle().Which.Should().Be(ApiKey);
    }

    [Fact]
    public void Endpoint_defaults_to_public_us_intake_with_logs_path()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new DatadogLogSink(ApiKey, Service, httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        handler.Requests[0].RequestUri!.ToString()
            .Should().Be("https://http-intake.logs.datadoghq.com/api/v2/logs");
    }

    [Fact]
    public void Endpoint_can_be_pointed_at_local_datadog_agent()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new DatadogLogSink(
            ApiKey, Service,
            endpoint: "http://localhost:8126",
            httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        handler.Requests[0].RequestUri!.ToString()
            .Should().Be("http://localhost:8126/api/v2/logs");
    }

    [Fact]
    public void Static_tags_merge_with_per_event_category_tag()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        var staticTags = new Dictionary<string, string>
        {
            ["env"] = "prod",
            ["version"] = "1.2.3",
        };
        using var sink = new DatadogLogSink(
            ApiKey, Service,
            staticTags: staticTags,
            httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        var ddtags = JsonDocument.Parse(handler.LastRequestBodyString!)
            .RootElement[0].GetProperty("ddtags").GetString()!;

        ddtags.Should().Contain("env:prod");
        ddtags.Should().Contain("version:1.2.3");
        ddtags.Should().Contain("category:");
    }

    [Fact]
    public void Exception_maps_to_error_triple()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new DatadogLogSink(ApiKey, Service, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithContext(LogContextKeys.Exception, new InvalidOperationException("boom"))
            .Build();

        sink.Log(evt);

        var entry = JsonDocument.Parse(handler.LastRequestBodyString!).RootElement[0];
        entry.GetProperty("error.message").GetString().Should().Be("boom");
        entry.GetProperty("error.kind").GetString().Should().Be("System.InvalidOperationException");
        entry.GetProperty("error.stack").GetString().Should().Contain("InvalidOperationException");
    }

    [Theory]
    [InlineData("trace", "debug")]
    [InlineData("info", "info")]
    [InlineData("warn", "warn")]
    [InlineData("error", "error")]
    [InlineData("critical", "critical")]
    [InlineData("fatal", "emergency")]
    public void Status_maps_herald_levels_to_datadog_vocabulary(string heraldKey, string expected)
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new DatadogLogSink(ApiKey, Service, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithLevel(new LogLevel(heraldKey, heraldKey))
            .Build();

        sink.Log(evt);

        var status = JsonDocument.Parse(handler.LastRequestBodyString!)
            .RootElement[0].GetProperty("status").GetString();
        status.Should().Be(expected);
    }

    [Fact]
    public void LogBatch_empty_is_a_no_op()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new DatadogLogSink(ApiKey, Service, httpClient: client);

        sink.LogBatch(Array.Empty<LogEvent>());

        handler.RequestCount.Should().Be(0);
    }
}
