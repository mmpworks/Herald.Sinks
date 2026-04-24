// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Net;
using System.Net.Http;
using FluentAssertions;
using Herald.Sinks.Loggly;
using Herald.Sinks.Loggly.Providers;
using MMP.Herald;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Loggly.Tests;

public sealed class LogglyLogSinkTests
{
    private const string Token = "loggly-customer-token";

    [Fact]
    public void Constructor_throws_on_null_token()
    {
        Action act = () => new LogglyLogSink(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Log_posts_to_bulk_endpoint_with_token_and_tag_in_url()
    {
        var handler = new TestHttpMessageHandler();
        handler.RespondWith(HttpStatusCode.OK);
        var client = new HttpClient(handler);
        using var sink = new LogglyLogSink(Token, tag: "myapp", httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        var req = handler.Requests[0];
        req.RequestUri!.AbsoluteUri.Should().StartWith($"https://logs-01.loggly.com/bulk/{Token}/tag/myapp/");
    }

    [Fact]
    public void Provider_sink_kind_is_loggly()
    {
        new LogglyLogSinkProvider().SinkKind.Should().Be("loggly");
        LogglyLogSinkProvider.KindKey.Should().Be("loggly");
    }
}
