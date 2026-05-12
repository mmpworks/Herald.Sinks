// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Herald.Sinks.Slack;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Slack.Tests;

public sealed class SlackWebhookLogSinkTests
{
    private static readonly TimeSpan ExpectedTimeout = TimeSpan.FromSeconds(30);
    private const string WebhookUrl = "https://hooks.slack.com/services/T00/B00/xxx";
    private readonly ILogLevelRegistry _registry = RegistryHelper.CreateDefault();

    [Fact]
    public void Null_webhook_url_throws()
    {
        var act = () => new SlackWebhookLogSink(null!, _registry);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Log_posts_json_payload_with_emoji_and_level()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SlackWebhookLogSink(WebhookUrl, _registry, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Warn)
            .WithMessage("Slow query on {table}", "Slow query on users")
            .Build();

        sink.Log(evt);

        handler.RequestCount.Should().Be(1);
        var doc = JsonDocument.Parse(handler.LastRequestBodyString!);
        var text = doc.RootElement.GetProperty("text").GetString()!;
        text.Should().Contain(":warning:");
        text.Should().Contain("Slow query on users");
    }

    [Fact]
    public void Message_with_slack_markup_is_entity_escaped()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new SlackWebhookLogSink(WebhookUrl, _registry, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithMessage("<script>", "<script>")
            .Build();

        sink.Log(evt);

        var text = JsonDocument.Parse(handler.LastRequestBodyString!).RootElement.GetProperty("text").GetString()!;
        text.Should().Contain("&lt;script&gt;");
    }

    [Fact]
    public void Owned_client_has_30s_timeout()
    {
        using var sink = new SlackWebhookLogSink(WebhookUrl, _registry);

        var field = typeof(SlackWebhookLogSink).GetField("_httpClient",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var client = (HttpClient)field.GetValue(sink)!;

        client.Timeout.Should().Be(ExpectedTimeout);
    }
}
