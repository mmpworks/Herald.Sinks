// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Herald.Sinks.NewRelicLogs;
using Herald.Sinks.NewRelicLogs.Providers;
using MMP.Herald;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.NewRelicLogs.Tests;

public sealed class NewRelicLogsLogSinkTests
{
    private const string Key = "NRAK-STUBLICENSEKEY";

    [Fact]
    public void Constructor_throws_on_null_license_key()
    {
        Action act = () => new NewRelicLogsLogSink(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Log_posts_json_array_with_api_key_header()
    {
        var handler = new TestHttpMessageHandler();
        handler.RespondWith(HttpStatusCode.OK);
        var client = new HttpClient(handler);
        using var sink = new NewRelicLogsLogSink(Key, httpClient: client);

        sink.Log(LogEventBuilder.Create().WithMessage("hi").Build());

        handler.RequestCount.Should().Be(1);
        handler.Requests[0].Headers.GetValues("Api-Key").Single().Should().Be(Key);

        using var doc = JsonDocument.Parse(handler.LastRequestBodyString!);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        var first = doc.RootElement[0];
        first.GetProperty("message").GetString().Should().Be("hi");
        first.GetProperty("attributes").GetProperty("level").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Provider_sink_kind_is_newrelic_logs()
    {
        new NewRelicLogsLogSinkProvider().SinkKind.Should().Be("newrelic_logs");
        NewRelicLogsLogSinkProvider.KindKey.Should().Be("newrelic_logs");
    }
}
