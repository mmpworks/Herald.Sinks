// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Net.Http;
using System.Reflection;
using FluentAssertions;
using Herald.Sinks.GenericWebhook;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.GenericWebhook.Tests;

public sealed class GenericWebhookLogSinkTests
{
    private static readonly TimeSpan ExpectedTimeout = TimeSpan.FromSeconds(30);
    private const string Url = "http://localhost:8080/webhook";
    private readonly ILogLevelRegistry _registry = RegistryHelper.CreateDefault();

    [Fact]
    public void Null_url_throws()
    {
        var act = () => new GenericWebhookLogSink(null!, _registry);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Log_posts_formatted_payload()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new GenericWebhookLogSink(Url, _registry, httpClient: client);

        var evt = LogEventBuilder.Create().WithMessage("hello", "hello").Build();
        sink.Log(evt);

        handler.RequestCount.Should().Be(1);
        handler.LastRequestBodyString.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Owned_client_has_30s_timeout()
    {
        using var sink = new GenericWebhookLogSink(Url, _registry);

        var field = typeof(GenericWebhookLogSink).GetField("_httpClient",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var client = (HttpClient)field.GetValue(sink)!;

        client.Timeout.Should().Be(ExpectedTimeout);
    }
}
