// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Herald.Sinks.MicrosoftTeams;
using Herald.Sinks.MicrosoftTeams.Providers;
using MMP.Herald;
using MMP.Herald.Levels;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.MicrosoftTeams.Tests;

public sealed class MicrosoftTeamsLogSinkTests
{
    private const string Hook = "https://example.webhook.office.com/webhookb2/stub";

    [Fact]
    public void Constructor_throws_on_null_webhook()
    {
        Action act = () => new MicrosoftTeamsLogSink(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Log_posts_message_card_with_theme_color()
    {
        var handler = new TestHttpMessageHandler();
        handler.RespondWith(HttpStatusCode.OK);
        var client = new HttpClient(handler);
        using var sink = new MicrosoftTeamsLogSink(Hook, httpClient: client);

        sink.Log(LogEventBuilder.Create().WithLevel(KnownLogLevels.Error).WithMessage("boom").Build());

        handler.RequestCount.Should().Be(1);
        using var doc = JsonDocument.Parse(handler.LastRequestBodyString!);
        doc.RootElement.GetProperty("@type").GetString().Should().Be("MessageCard");
        doc.RootElement.GetProperty("themeColor").GetString().Should().Be("D73A49");  // red for error
        doc.RootElement.GetProperty("text").GetString().Should().Be("boom");
    }

    [Fact]
    public void Provider_sink_kind_is_ms_teams()
    {
        new MicrosoftTeamsLogSinkProvider().SinkKind.Should().Be("ms_teams");
        MicrosoftTeamsLogSinkProvider.KindKey.Should().Be("ms_teams");
    }

    [Fact]
    public void Provider_is_community_edition()
    {
        new MicrosoftTeamsLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
