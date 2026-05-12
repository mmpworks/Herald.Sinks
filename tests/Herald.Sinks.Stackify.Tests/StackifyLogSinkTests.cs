// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Herald.Sinks.Stackify;
using Herald.Sinks.Stackify.Providers;
using MMP.Herald;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Stackify.Tests;

public sealed class StackifyLogSinkTests
{
    private const string Key = "STACKIFY-KEY";

    [Fact]
    public void Constructor_throws_on_null_key()
    {
        Action act = () => new StackifyLogSink(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Log_posts_envelope_with_msgs_array_and_x_stackify_key()
    {
        var handler = new TestHttpMessageHandler();
        handler.RespondWith(HttpStatusCode.OK);
        var client = new HttpClient(handler);
        using var sink = new StackifyLogSink(Key, appName: "myapp", environmentName: "stage", httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        handler.Requests[0].Headers.GetValues("X-Stackify-Key").Single().Should().Be(Key);
        using var doc = JsonDocument.Parse(handler.LastRequestBodyString!);
        doc.RootElement.GetProperty("AppName").GetString().Should().Be("myapp");
        doc.RootElement.GetProperty("Env").GetString().Should().Be("stage");
        doc.RootElement.GetProperty("Msgs").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public void Provider_sink_kind_is_stackify()
    {
        new StackifyLogSinkProvider().SinkKind.Should().Be("stackify");
        StackifyLogSinkProvider.KindKey.Should().Be("stackify");
    }
}
