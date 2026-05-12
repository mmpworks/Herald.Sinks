// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using FluentAssertions;
using Herald.Sinks.SumoLogic;
using Herald.Sinks.SumoLogic.Providers;
using MMP.Herald;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.SumoLogic.Tests;

public sealed class SumoLogicLogSinkTests
{
    private const string Url = "https://endpoint1.collection.us2.sumologic.com/receiver/v1/http/STUB";

    [Fact]
    public void Constructor_throws_on_null_url()
    {
        Action act = () => new SumoLogicLogSink(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Log_posts_to_source_url_with_optional_sumo_headers()
    {
        var handler = new TestHttpMessageHandler();
        handler.RespondWith(HttpStatusCode.OK);
        var client = new HttpClient(handler);
        using var sink = new SumoLogicLogSink(Url, sourceCategory: "myapp/prod", sourceHost: "host-01", httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        var req = handler.Requests[0];
        req.RequestUri!.AbsoluteUri.Should().Be(Url);
        req.Headers.GetValues("X-Sumo-Category").Single().Should().Be("myapp/prod");
        req.Headers.GetValues("X-Sumo-Host").Single().Should().Be("host-01");
    }

    [Fact]
    public void Provider_sink_kind_is_sumologic()
    {
        new SumoLogicLogSinkProvider().SinkKind.Should().Be("sumologic");
        SumoLogicLogSinkProvider.KindKey.Should().Be("sumologic");
    }
}
