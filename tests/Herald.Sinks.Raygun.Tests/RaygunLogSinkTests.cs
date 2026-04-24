// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Herald.Sinks.Raygun;
using Herald.Sinks.Raygun.Providers;
using MMP.Herald;
using MMP.Herald.Levels;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Raygun.Tests;

public sealed class RaygunLogSinkTests
{
    private const string Key = "RAYGUN-STUB-KEY";

    [Fact]
    public void Constructor_throws_on_null_key()
    {
        Action act = () => new RaygunLogSink(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Log_posts_entries_with_x_apikey_header()
    {
        var handler = new TestHttpMessageHandler();
        handler.RespondWith(HttpStatusCode.Accepted);
        var client = new HttpClient(handler);
        using var sink = new RaygunLogSink(Key, httpClient: client);

        sink.Log(LogEventBuilder.Create().WithLevel(KnownLogLevels.Error).WithMessage("boom").Build());

        handler.RequestCount.Should().Be(1);
        handler.Requests[0].RequestUri!.AbsoluteUri.Should().Be("https://api.raygun.com/entries");
        handler.Requests[0].Headers.GetValues("X-ApiKey").Single().Should().Be(Key);

        using var doc = JsonDocument.Parse(handler.LastRequestBodyString!);
        doc.RootElement.GetProperty("details").GetProperty("error").GetProperty("message").GetString().Should().Be("boom");
    }

    [Fact]
    public void Provider_sink_kind_is_raygun()
    {
        new RaygunLogSinkProvider().SinkKind.Should().Be("raygun");
        RaygunLogSinkProvider.KindKey.Should().Be("raygun");
    }
}
