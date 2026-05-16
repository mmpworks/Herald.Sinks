// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Herald.Sinks.Dynatrace;
using Herald.Sinks.Dynatrace.Providers;
using MMP.Herald.Levels;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Dynatrace.Tests;

public sealed class DynatraceLogSinkTests
{
    private const string EnvUrl = "https://abc123.live.dynatrace.com";
    private const string Token = "dt0c01.STUBTOKEN";

    [Fact]
    public void Constructor_throws_on_null_environment_url()
    {
        Action act = () => new DynatraceLogSink(null!, Token);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_api_token()
    {
        Action act = () => new DynatraceLogSink(EnvUrl, null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Log_posts_json_array_with_api_token_header_and_ingest_path()
    {
        var handler = new TestHttpMessageHandler();
        handler.RespondWith(HttpStatusCode.OK);
        var client = new HttpClient(handler);
        using var sink = new DynatraceLogSink(EnvUrl, Token, client);

        sink.Log(LogEventBuilder.Create().WithLevel(KnownLogLevels.Warn).WithMessage("hi").Build());

        handler.RequestCount.Should().Be(1);
        var req = handler.Requests[0];
        req.Method.Should().Be(HttpMethod.Post);
        req.RequestUri!.AbsolutePath.Should().Be("/api/v2/logs/ingest");
        req.Headers.GetValues("Authorization").Single().Should().Be("Api-Token " + Token);

        using var doc = JsonDocument.Parse(handler.LastRequestBodyString!);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        var first = doc.RootElement[0];
        first.GetProperty("severity").GetString().Should().Be("WARN");
        first.GetProperty("content").GetString().Should().Be("hi");
    }

    [Fact]
    public void Provider_sink_kind_is_dynatrace()
    {
        new DynatraceLogSinkProvider().SinkKind.Should().Be("dynatrace");
        DynatraceLogSinkProvider.KindKey.Should().Be("dynatrace");
    }

}
