// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Herald.Sinks.ApplicationInsightsHttp;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Services;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.ApplicationInsightsHttp.Tests;

public sealed class ApplicationInsightsConnectionStringTests
{
    [Fact]
    public void Parse_handles_bare_instrumentation_key()
    {
        var parsed = ApplicationInsightsConnectionString.Parse(
            "abcdef01-2345-6789-abcd-ef0123456789");

        parsed.InstrumentationKey.Should().Be("abcdef01-2345-6789-abcd-ef0123456789");
        parsed.IngestionEndpoint.ToString().Should().Be(
            ApplicationInsightsConnectionString.DefaultIngestionEndpoint);
        parsed.TrackEndpoint.ToString().Should().EndWith("v2.1/track");
    }

    [Fact]
    public void Parse_handles_full_connection_string()
    {
        var parsed = ApplicationInsightsConnectionString.Parse(
            "InstrumentationKey=00000000-0000-0000-0000-000000000001;" +
            "IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;" +
            "LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/");

        parsed.InstrumentationKey.Should().Be("00000000-0000-0000-0000-000000000001");
        parsed.IngestionEndpoint.ToString().Should().Be(
            "https://eastus-8.in.applicationinsights.azure.com/");
        parsed.TrackEndpoint.ToString().Should().Be(
            "https://eastus-8.in.applicationinsights.azure.com/v2.1/track");
    }

    [Fact]
    public void Parse_normalizes_endpoint_without_trailing_slash()
    {
        var parsed = ApplicationInsightsConnectionString.Parse(
            "InstrumentationKey=abc;IngestionEndpoint=https://host.example.com");

        parsed.IngestionEndpoint.ToString().Should().EndWith("/");
        parsed.TrackEndpoint.ToString().Should().Be("https://host.example.com/v2.1/track");
    }

    [Fact]
    public void Parse_throws_on_missing_instrumentation_key()
    {
        var act = () => ApplicationInsightsConnectionString.Parse(
            "IngestionEndpoint=https://host.example.com/");

        act.Should().Throw<FormatException>()
            .WithMessage("*InstrumentationKey*");
    }

    [Fact]
    public void Parse_throws_on_empty_input()
    {
        var act = () => ApplicationInsightsConnectionString.Parse("");

        act.Should().Throw<ArgumentException>();
    }
}

public sealed class ApplicationInsightsSeverityMapperTests
{
    [Theory]
    [InlineData("trace", ApplicationInsightsSeverityMapper.Verbose)]
    [InlineData("debug", ApplicationInsightsSeverityMapper.Verbose)]
    [InlineData("info", ApplicationInsightsSeverityMapper.Information)]
    [InlineData("notice", ApplicationInsightsSeverityMapper.Information)]
    [InlineData("metric", ApplicationInsightsSeverityMapper.Information)]
    [InlineData("success", ApplicationInsightsSeverityMapper.Information)]
    [InlineData("warn", ApplicationInsightsSeverityMapper.Warning)]
    [InlineData("error", ApplicationInsightsSeverityMapper.Error)]
    [InlineData("security", ApplicationInsightsSeverityMapper.Error)]
    [InlineData("critical", ApplicationInsightsSeverityMapper.Critical)]
    [InlineData("fatal", ApplicationInsightsSeverityMapper.Critical)]
    public void MapSeverityLevel_projects_each_canonical_level(string key, int expected)
    {
        var level = new LogLevel(key, key);

        ApplicationInsightsSeverityMapper.MapSeverityLevel(level).Should().Be(expected);
    }

    [Fact]
    public void MapSeverityLevel_falls_back_to_information_for_unknown_level()
    {
        var custom = new LogLevel("telemetry_weird", "Weird");

        ApplicationInsightsSeverityMapper.MapSeverityLevel(custom)
            .Should().Be(ApplicationInsightsSeverityMapper.Information);
    }
}

public sealed class ApplicationInsightsLogSinkTests
{
    private const string ConnectionString =
        "InstrumentationKey=11111111-1111-1111-1111-111111111111;" +
        "IngestionEndpoint=https://fake.example.com/";

    [Fact]
    public void Log_posts_a_message_envelope_with_rendered_text()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new ApplicationInsightsLogSink(ConnectionString, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Info)
            .WithMessage("User {Name} signed in", "User Alice signed in")
            .Build();

        sink.Log(evt);

        handler.RequestCount.Should().Be(1);
        var doc = JsonDocument.Parse(handler.LastRequestBodyString!);
        var root = doc.RootElement[0];

        root.GetProperty("name").GetString().Should().Be("Microsoft.ApplicationInsights.Message");
        root.GetProperty("iKey").GetString().Should().Be("11111111-1111-1111-1111-111111111111");
        root.GetProperty("data").GetProperty("baseType").GetString().Should().Be("MessageData");
        root.GetProperty("data").GetProperty("baseData")
            .GetProperty("message").GetString().Should().Be("User Alice signed in");
    }

    [Fact]
    public void Log_posts_to_the_v2_1_track_endpoint()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new ApplicationInsightsLogSink(ConnectionString, httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        handler.Requests[0].RequestUri!.ToString()
            .Should().Be("https://fake.example.com/v2.1/track");
    }

    [Fact]
    public void Log_maps_level_to_application_insights_severity()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new ApplicationInsightsLogSink(ConnectionString, httpClient: client);

        sink.Log(LogEventBuilder.Create().WithLevel(KnownLogLevels.Error).Build());

        var doc = JsonDocument.Parse(handler.LastRequestBodyString!);
        doc.RootElement[0].GetProperty("data").GetProperty("baseData")
            .GetProperty("severityLevel").GetInt32()
            .Should().Be(ApplicationInsightsSeverityMapper.Error);
    }

    [Fact]
    public void Log_emits_role_name_tag_when_supplied()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new ApplicationInsightsLogSink(
            ConnectionString, roleName: "OrderService", httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        var doc = JsonDocument.Parse(handler.LastRequestBodyString!);
        doc.RootElement[0].GetProperty("tags")
            .GetProperty("ai.cloud.role").GetString().Should().Be("OrderService");
    }

    [Fact]
    public void Log_projects_trace_context_into_ai_operation_tags()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new ApplicationInsightsLogSink(ConnectionString, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithContext(LogContextKeys.TraceId, "abcd1234abcd1234abcd1234abcd1234")
            .WithContext(LogContextKeys.SpanId, "1111222233334444")
            .Build();

        sink.Log(evt);

        var tags = JsonDocument.Parse(handler.LastRequestBodyString!).RootElement[0].GetProperty("tags");
        tags.GetProperty("ai.operation.id").GetString().Should().Be("abcd1234abcd1234abcd1234abcd1234");
        tags.GetProperty("ai.operation.parentId").GetString().Should().Be("1111222233334444");
    }

    [Fact]
    public void Log_projects_properties_into_customDimensions()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new ApplicationInsightsLogSink(ConnectionString, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithProperty("UserId", 42)
            .WithProperty("Region", "eastus")
            .Build();

        sink.Log(evt);

        var props = JsonDocument.Parse(handler.LastRequestBodyString!)
            .RootElement[0].GetProperty("data").GetProperty("baseData").GetProperty("properties");
        props.GetProperty("UserId").GetString().Should().Be("42");
        props.GetProperty("Region").GetString().Should().Be("eastus");
        props.GetProperty("category").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Log_flattens_exception_context_into_properties()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new ApplicationInsightsLogSink(ConnectionString, httpClient: client);

        var boom = new InvalidOperationException("boom");
        var evt = LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Error)
            .WithContext(LogContextKeys.Exception, boom)
            .Build();

        sink.Log(evt);

        var props = JsonDocument.Parse(handler.LastRequestBodyString!)
            .RootElement[0].GetProperty("data").GetProperty("baseData").GetProperty("properties");
        props.GetProperty("exception.type").GetString().Should().Contain("InvalidOperationException");
        props.GetProperty("exception.message").GetString().Should().Be("boom");
    }

    [Fact]
    public void LogBatch_sends_one_request_with_multiple_envelopes()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new ApplicationInsightsLogSink(ConnectionString, httpClient: client);

        var events = new[]
        {
            LogEventBuilder.Create().WithMessage("A", "A").Build(),
            LogEventBuilder.Create().WithMessage("B", "B").Build(),
            LogEventBuilder.Create().WithMessage("C", "C").Build()
        };

        sink.LogBatch(events);

        handler.RequestCount.Should().Be(1);
        var doc = JsonDocument.Parse(handler.LastRequestBodyString!);
        doc.RootElement.GetArrayLength().Should().Be(3);
    }

    [Fact]
    public void LogBatch_with_empty_list_is_a_no_op()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new ApplicationInsightsLogSink(ConnectionString, httpClient: client);

        sink.LogBatch(Array.Empty<LogEvent>());

        handler.RequestCount.Should().Be(0);
    }
}
