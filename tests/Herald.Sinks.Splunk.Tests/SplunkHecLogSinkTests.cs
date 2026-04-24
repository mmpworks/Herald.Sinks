// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Herald.Sinks.Splunk;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Services;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Splunk.Tests;

public sealed class SplunkLevelMapperTests
{
    [Theory]
    [InlineData("trace", "TRACE")]
    [InlineData("debug", "DEBUG")]
    [InlineData("info", "INFO")]
    [InlineData("warn", "WARN")]
    [InlineData("error", "ERROR")]
    [InlineData("critical", "FATAL")]
    [InlineData("fatal", "FATAL")]
    public void MapLevel_projects_canonical_levels(string key, string expected)
    {
        var level = new LogLevel(key, key);
        SplunkLevelMapper.MapLevel(level).Should().Be(expected);
    }
}

public sealed class SplunkHecLogSinkTests
{
    private const string HecUrl = "https://splunk.example.com:8088/services/collector/event";
    private const string Token = "test-hec-token";

    [Fact]
    public void Log_posts_hec_envelope_with_event_payload()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SplunkHecLogSink(HecUrl, Token, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Warn)
            .WithMessage("Low disk on {host}", "Low disk on nodeA")
            .Build();

        sink.Log(evt);

        handler.RequestCount.Should().Be(1);
        var body = handler.LastRequestBodyString!.TrimEnd('\n');
        var doc = JsonDocument.Parse(body);

        doc.RootElement.GetProperty("time").GetDouble().Should().BeGreaterThan(0);
        var ev = doc.RootElement.GetProperty("event");
        ev.GetProperty("message").GetString().Should().Be("Low disk on nodeA");
        ev.GetProperty("messageTemplate").GetString().Should().Be("Low disk on {host}");
        ev.GetProperty("severity").GetString().Should().Be("WARN");
        ev.GetProperty("category").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Log_sets_splunk_auth_header()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SplunkHecLogSink(HecUrl, Token, httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        var auth = handler.Requests[0].Headers.Authorization!;
        auth.Scheme.Should().Be("Splunk");
        auth.Parameter.Should().Be(Token);
    }

    [Fact]
    public void Log_emits_envelope_fields_when_supplied()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SplunkHecLogSink(
            HecUrl, Token,
            host: "nodeA",
            source: "herald",
            sourceType: "herald_json",
            index: "logs",
            httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        var body = handler.LastRequestBodyString!.TrimEnd('\n');
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("host").GetString().Should().Be("nodeA");
        doc.RootElement.GetProperty("source").GetString().Should().Be("herald");
        doc.RootElement.GetProperty("sourcetype").GetString().Should().Be("herald_json");
        doc.RootElement.GetProperty("index").GetString().Should().Be("logs");
    }

    [Fact]
    public void Log_emits_exception_inside_event()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SplunkHecLogSink(HecUrl, Token, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Error)
            .WithContext(LogContextKeys.Exception, new InvalidOperationException("boom"))
            .Build();

        sink.Log(evt);

        var body = handler.LastRequestBodyString!.TrimEnd('\n');
        var ev = JsonDocument.Parse(body).RootElement.GetProperty("event");
        ev.GetProperty("exception").GetString().Should().Contain("boom");
    }

    [Fact]
    public void Log_emits_properties_inside_event()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SplunkHecLogSink(HecUrl, Token, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithProperty("UserId", 7)
            .WithProperty("Region", "eastus")
            .Build();

        sink.Log(evt);

        var body = handler.LastRequestBodyString!.TrimEnd('\n');
        var ev = JsonDocument.Parse(body).RootElement.GetProperty("event");
        ev.GetProperty("UserId").GetInt32().Should().Be(7);
        ev.GetProperty("Region").GetString().Should().Be("eastus");
    }

    [Fact]
    public void LogBatch_concatenates_event_envelopes()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SplunkHecLogSink(HecUrl, Token, httpClient: client);

        var events = new[]
        {
            LogEventBuilder.Create().WithMessage("A", "A").Build(),
            LogEventBuilder.Create().WithMessage("B", "B").Build()
        };

        sink.LogBatch(events);

        handler.RequestCount.Should().Be(1);
        var lines = handler.LastRequestBodyString!
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2);
        foreach (var line in lines)
        {
            JsonDocument.Parse(line); // each envelope must be valid JSON
        }
    }

    [Fact]
    public void LogBatch_with_empty_list_is_a_no_op()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SplunkHecLogSink(HecUrl, Token, httpClient: client);

        sink.LogBatch(Array.Empty<LogEvent>());

        handler.RequestCount.Should().Be(0);
    }

    [Fact]
    public void Constructor_appends_default_hec_path_when_only_host_given()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SplunkHecLogSink(
            "https://splunk.example.com:8088",
            Token,
            httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        handler.Requests[0].RequestUri!.ToString()
            .Should().EndWith("/services/collector/event");
    }
}
