// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using FluentAssertions;
using Herald.Sinks.ElmahIo;
using Herald.Sinks.ElmahIo.Providers;
using MMP.Herald;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.ElmahIo.Tests;

public sealed class ElmahIoLogSinkTests
{
    private const string Key = "elmahio-stub-key";
    private const string LogId = "00000000-0000-0000-0000-000000000000";

    [Fact]
    public void Constructor_throws_on_null_key()
    {
        Action act = () => new ElmahIoLogSink(null!, LogId);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_log_id()
    {
        Action act = () => new ElmahIoLogSink(Key, null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Log_posts_to_bulk_endpoint_with_bearer_auth()
    {
        var handler = new TestHttpMessageHandler();
        handler.RespondWith(HttpStatusCode.OK);
        var client = new HttpClient(handler);
        using var sink = new ElmahIoLogSink(Key, LogId, client);

        sink.Log(LogEventBuilder.Create().Build());

        var req = handler.Requests[0];
        req.RequestUri!.AbsoluteUri.Should().Be($"https://api.elmah.io/v3/messages/{LogId}/bulk");
        req.Headers.GetValues("Authorization").Single().Should().Be("Bearer " + Key);
    }

    [Fact]
    public void Provider_sink_kind_is_elmahio()
    {
        new ElmahIoLogSinkProvider().SinkKind.Should().Be("elmahio");
        ElmahIoLogSinkProvider.KindKey.Should().Be("elmahio");
    }
}
