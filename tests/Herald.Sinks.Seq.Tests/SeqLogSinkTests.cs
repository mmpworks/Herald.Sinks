// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Herald.Sinks.Seq;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Services;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Seq.Tests;

public sealed class SeqLevelMapperTests
{
    [Theory]
    [InlineData("trace", "Verbose")]
    [InlineData("debug", "Debug")]
    [InlineData("warn", "Warning")]
    [InlineData("error", "Error")]
    [InlineData("security", "Error")]
    [InlineData("critical", "Fatal")]
    [InlineData("fatal", "Fatal")]
    public void MapLevel_projects_each_canonical_level(string key, string expected)
    {
        var level = new LogLevel(key, key);
        SeqLevelMapper.MapLevel(level).Should().Be(expected);
    }

    [Theory]
    [InlineData("info")]
    [InlineData("notice")]
    [InlineData("metric")]
    [InlineData("success")]
    public void MapLevel_returns_null_for_clef_default_levels(string key)
    {
        var level = new LogLevel(key, key);
        SeqLevelMapper.MapLevel(level).Should().BeNull();
    }
}

public sealed class SeqLogSinkTests
{
    private const string Server = "http://fake-seq.example.com";

    [Fact]
    public void Log_posts_clef_line_with_required_fields()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SeqLogSink(Server, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Error)
            .WithMessage("User {name} failed", "User Alice failed")
            .Build();

        sink.Log(evt);

        handler.RequestCount.Should().Be(1);
        handler.Requests[0].RequestUri!.ToString()
            .Should().EndWith("/api/events/raw?clef");

        // CLEF is newline-delimited JSON — one event, one line, trailing \n.
        var body = handler.LastRequestBodyString!.TrimEnd('\n');
        var doc = JsonDocument.Parse(body);

        doc.RootElement.GetProperty("@m").GetString().Should().Be("User Alice failed");
        doc.RootElement.GetProperty("@mt").GetString().Should().Be("User {name} failed");
        doc.RootElement.GetProperty("@l").GetString().Should().Be("Error");
        doc.RootElement.GetProperty("@t").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Log_omits_level_for_information_events()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SeqLogSink(Server, httpClient: client);

        sink.Log(LogEventBuilder.Create().WithLevel(KnownLogLevels.Info).Build());

        var body = handler.LastRequestBodyString!.TrimEnd('\n');
        var doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("@l", out _).Should().BeFalse(
            "CLEF omits @l for default Information level");
    }

    [Fact]
    public void Log_sets_content_type_to_clef()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SeqLogSink(Server, httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        handler.Requests[0].Content!.Headers.ContentType!.MediaType
            .Should().Be(SeqLogSink.ClefMediaType);
    }

    [Fact]
    public void Log_attaches_api_key_header_when_set()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SeqLogSink(Server, apiKey: "secret-key", httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        handler.Requests[0].Headers.GetValues("X-Seq-ApiKey")
            .Should().Contain("secret-key");
    }

    [Fact]
    public void Log_emits_exception_as_x_field()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SeqLogSink(Server, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Error)
            .WithContext(LogContextKeys.Exception, new InvalidOperationException("boom"))
            .Build();

        sink.Log(evt);

        var body = handler.LastRequestBodyString!.TrimEnd('\n');
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("@x").GetString().Should().Contain("boom");
    }

    [Fact]
    public void Log_projects_properties_and_source_context()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SeqLogSink(Server, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithProperty("UserId", 42)
            .WithProperty("Region", "eastus")
            .Build();

        sink.Log(evt);

        var body = handler.LastRequestBodyString!.TrimEnd('\n');
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("UserId").GetInt32().Should().Be(42);
        doc.RootElement.GetProperty("Region").GetString().Should().Be("eastus");
        doc.RootElement.GetProperty("SourceContext").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void LogBatch_sends_newline_delimited_clef()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SeqLogSink(Server, httpClient: client);

        var events = new[]
        {
            LogEventBuilder.Create().WithMessage("A", "A").Build(),
            LogEventBuilder.Create().WithMessage("B", "B").Build(),
            LogEventBuilder.Create().WithMessage("C", "C").Build()
        };

        sink.LogBatch(events);

        handler.RequestCount.Should().Be(1);
        var lines = handler.LastRequestBodyString!
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(3);
        foreach (var line in lines)
        {
            JsonDocument.Parse(line); // each line must parse as valid JSON
        }
    }

    [Fact]
    public void LogBatch_with_empty_list_is_a_no_op()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SeqLogSink(Server, httpClient: client);

        sink.LogBatch(Array.Empty<LogEvent>());

        handler.RequestCount.Should().Be(0);
    }
}
