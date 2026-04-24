// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using FluentAssertions;
using Herald.Sinks.HttpJson;
using MMP.Herald.Levels;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.HttpJson.Tests;

public sealed class HttpJsonLogSinkTests
{
    private static readonly TimeSpan ExpectedTimeout = TimeSpan.FromSeconds(30);
    private readonly ILogLevelRegistry _registry = RegistryHelper.CreateDefault();

    [Fact]
    public void Log_sends_http_post()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        var sink = new HttpJsonLogSink("http://localhost:9200/logs", _registry, httpClient: client);

        var evt = LogEventBuilder.Create().WithLevel(KnownLogLevels.Info).WithMessage("Test log").Build();
        sink.Log(evt);

        handler.RequestCount.Should().Be(1);
        handler.LastRequestBodyString.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void LogBatch_sends_all_events()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        var sink = new HttpJsonLogSink("http://localhost:9200/logs", _registry, httpClient: client);

        var events = new[]
        {
            LogEventBuilder.Create().WithMessage("Event 1").Build(),
            LogEventBuilder.Create().WithMessage("Event 2").Build()
        };
        sink.LogBatch(events);

        handler.RequestCount.Should().BeGreaterOrEqualTo(1);
        handler.LastRequestBodyString.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Throws_on_server_error()
    {
        var handler = new TestHttpMessageHandler();
        handler.RespondWith(HttpStatusCode.InternalServerError);
        var client = new HttpClient(handler);
        var sink = new HttpJsonLogSink("http://localhost:9200/logs", _registry, httpClient: client);

        var evt = LogEventBuilder.Create().Build();

        var act = () => sink.Log(evt);
        act.Should().Throw<HttpRequestException>();
    }

    [Fact]
    public void Dispose_does_not_throw()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        var sink = new HttpJsonLogSink("http://localhost:9200/logs", _registry, httpClient: client);

        var act = () => sink.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Owned_client_has_30s_timeout()
    {
        using var sink = new HttpJsonLogSink("http://localhost:9200/logs", _registry);

        var field = typeof(HttpJsonLogSink).GetField("_httpClient",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var client = (HttpClient)field.GetValue(sink)!;

        client.Timeout.Should().Be(ExpectedTimeout);
    }

    [Fact]
    public void Caller_provided_client_preserves_timeout()
    {
        var customTimeout = TimeSpan.FromSeconds(10);
        using var externalClient = new HttpClient { Timeout = customTimeout };
        using var sink = new HttpJsonLogSink("http://localhost:9200/logs", _registry, httpClient: externalClient);

        var field = typeof(HttpJsonLogSink).GetField("_httpClient",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var client = (HttpClient)field.GetValue(sink)!;

        client.Timeout.Should().Be(customTimeout);
    }
}
