// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Net;
using System.Net.Http;
using FluentAssertions;
using Herald.Sinks.LogzIo;
using Herald.Sinks.LogzIo.Providers;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.LogzIo.Tests;

public sealed class LogzIoLogSinkTests
{
    private const string Token = "LOGZ-IO-ACCOUNT-TOKEN";

    [Fact]
    public void Constructor_throws_on_null_token()
    {
        Action act = () => new LogzIoLogSink(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Log_posts_ndjson_with_token_and_type_in_url()
    {
        var handler = new TestHttpMessageHandler();
        handler.RespondWith(HttpStatusCode.OK);
        var client = new HttpClient(handler);
        using var sink = new LogzIoLogSink(accountToken: Token, type: "myapp", httpClient: client);

        sink.Log(LogEventBuilder.Create().WithMessage("hi").Build());
        sink.Log(LogEventBuilder.Create().WithMessage("there").Build());

        handler.RequestCount.Should().Be(2);
        var req = handler.Requests[0];
        req.RequestUri!.Query.Should().Contain("token=" + Token).And.Contain("type=myapp");

        var body = handler.LastRequestBodyString!;
        body.Should().EndWith("\n");
    }

    [Fact]
    public void Provider_sink_kind_is_logzio()
    {
        new LogzIoLogSinkProvider().SinkKind.Should().Be("logzio");
        LogzIoLogSinkProvider.KindKey.Should().Be("logzio");
    }

}
