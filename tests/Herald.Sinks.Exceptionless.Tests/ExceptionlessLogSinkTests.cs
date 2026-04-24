// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Herald.Sinks.Exceptionless;
using Herald.Sinks.Exceptionless.Providers;
using MMP.Herald;
using MMP.Herald.Levels;
using MMP.Herald.Services;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Exceptionless.Tests;

public sealed class ExceptionlessLogSinkTests
{
    private const string Key = "EXLESS-STUB-KEY";

    [Fact]
    public void Constructor_throws_on_null_key()
    {
        Action act = () => new ExceptionlessLogSink(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Log_posts_array_with_log_type_when_no_exception()
    {
        var handler = new TestHttpMessageHandler();
        handler.RespondWith(HttpStatusCode.OK);
        var client = new HttpClient(handler);
        using var sink = new ExceptionlessLogSink(Key, httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        handler.Requests[0].Headers.GetValues("Authorization").Single().Should().Be("Bearer " + Key);
        using var doc = JsonDocument.Parse(handler.LastRequestBodyString!);
        doc.RootElement[0].GetProperty("type").GetString().Should().Be("log");
    }

    [Fact]
    public void Log_emits_error_type_when_exception_in_context()
    {
        var handler = new TestHttpMessageHandler();
        handler.RespondWith(HttpStatusCode.OK);
        var client = new HttpClient(handler);
        using var sink = new ExceptionlessLogSink(Key, httpClient: client);

        sink.Log(LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Error)
            .WithContext(LogContextKeys.Exception, new InvalidOperationException("boom"))
            .Build());

        using var doc = JsonDocument.Parse(handler.LastRequestBodyString!);
        doc.RootElement[0].GetProperty("type").GetString().Should().Be("error");
    }

    [Fact]
    public void Provider_sink_kind_is_exceptionless()
    {
        new ExceptionlessLogSinkProvider().SinkKind.Should().Be("exceptionless");
        ExceptionlessLogSinkProvider.KindKey.Should().Be("exceptionless");
    }
}
