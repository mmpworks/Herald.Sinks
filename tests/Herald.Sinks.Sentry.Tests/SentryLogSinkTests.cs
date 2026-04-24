// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Herald.Sinks.Sentry;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Services;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Sentry.Tests;

public sealed class SentryLogSinkTests
{
    private const string Dsn = "https://publickey@o42.ingest.sentry.io/1234";

    [Fact]
    public void Log_posts_event_with_message_and_level()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SentryLogSink(Dsn, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Error)
            .WithMessage("crash", "crash")
            .Build();

        sink.Log(evt);

        handler.RequestCount.Should().Be(1);
        var doc = JsonDocument.Parse(handler.LastRequestBodyString!);
        doc.RootElement.GetProperty("event_id").GetString().Should().NotBeNullOrWhiteSpace();
        doc.RootElement.GetProperty("level").GetString().Should().Be("error");
        doc.RootElement.GetProperty("platform").GetString().Should().Be("csharp");
        doc.RootElement.GetProperty("message").GetProperty("message").GetString().Should().Be("crash");
    }

    [Fact]
    public void Log_sets_x_sentry_auth_header_with_public_key()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SentryLogSink(Dsn, httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        var auth = handler.Requests[0].Headers.GetValues("X-Sentry-Auth").Should().ContainSingle().Subject;
        auth.Should().StartWith("Sentry sentry_version=7");
        auth.Should().Contain("sentry_key=publickey");
    }

    [Fact]
    public void Endpoint_resolves_to_api_project_store_path()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SentryLogSink(Dsn, httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        handler.Requests[0].RequestUri!.ToString().Should().EndWith("/api/1234/store/");
    }

    [Fact]
    public void Category_becomes_a_sentry_tag()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SentryLogSink(Dsn, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithCategory(new LogCategory("Combat"))
            .Build();

        sink.Log(evt);

        var tags = JsonDocument.Parse(handler.LastRequestBodyString!).RootElement.GetProperty("tags");
        tags.GetProperty("category").GetString().Should().Be("Combat");
    }

    [Fact]
    public void Exception_surfaces_under_exception_values()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SentryLogSink(Dsn, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithContext(LogContextKeys.Exception, new InvalidOperationException("boom"))
            .Build();

        sink.Log(evt);

        var values = JsonDocument.Parse(handler.LastRequestBodyString!)
            .RootElement.GetProperty("exception").GetProperty("values");
        values.GetArrayLength().Should().Be(1);
        var first = values[0];
        first.GetProperty("type").GetString().Should().Be("System.InvalidOperationException");
        first.GetProperty("value").GetString().Should().Be("boom");
    }

    [Theory]
    [InlineData("trace", "debug")]
    [InlineData("info", "info")]
    [InlineData("warn", "warning")]
    [InlineData("error", "error")]
    [InlineData("fatal", "fatal")]
    public void Level_mapping_uses_sentry_vocabulary(string heraldKey, string expected)
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SentryLogSink(Dsn, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithLevel(new LogLevel(heraldKey, heraldKey))
            .Build();

        sink.Log(evt);

        var level = JsonDocument.Parse(handler.LastRequestBodyString!)
            .RootElement.GetProperty("level").GetString();
        level.Should().Be(expected);
    }

    [Fact]
    public void Malformed_dsn_throws()
    {
        var act = () => new SentryLogSink("not-a-url");
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Dsn_without_public_key_throws()
    {
        var act = () => new SentryLogSink("https://sentry.example.com/1234");
        act.Should().Throw<ArgumentException>().WithMessage("*public key*");
    }
}
