// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using FluentAssertions;
using Herald.Sinks.OpenSearch;
using Herald.Sinks.OpenSearch.Providers;
using MMP.Herald;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.OpenSearch.Tests;

public sealed class OpenSearchLogSinkTests
{
    private const string Endpoint = "https://opensearch.example.com";

    [Fact]
    public void Constructor_throws_on_null_endpoint()
    {
        Action act = () => new OpenSearchLogSink(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Log_posts_bulk_ndjson_with_action_and_document_lines()
    {
        var handler = new TestHttpMessageHandler();
        handler.RespondWith(HttpStatusCode.OK, "{\"errors\":false}");
        var client = new HttpClient(handler);
        using var sink = new OpenSearchLogSink(Endpoint, httpClient: client);

        sink.Log(LogEventBuilder.Create().WithMessage("hi").Build());

        handler.RequestCount.Should().Be(1);
        var req = handler.Requests[0];
        req.Method.Should().Be(HttpMethod.Post);
        req.RequestUri!.AbsolutePath.Should().Be("/_bulk");

        var body = handler.LastRequestBodyString!;
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2);
        lines[0].Should().StartWith("{\"index\":");
        lines[0].Should().Contain("herald-logs-");
    }

    [Fact]
    public void Basic_auth_header_is_set_when_credentials_supplied()
    {
        var handler = new TestHttpMessageHandler();
        handler.RespondWith(HttpStatusCode.OK, "{}");
        var client = new HttpClient(handler);
        using var sink = new OpenSearchLogSink(Endpoint, username: "u", password: "p", httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        var auth = handler.Requests[0].Headers.Authorization;
        auth.Should().NotBeNull();
        auth!.Scheme.Should().Be("Basic");
    }

    [Fact]
    public void Provider_sink_kind_is_opensearch()
    {
        new OpenSearchLogSinkProvider().SinkKind.Should().Be("opensearch");
        OpenSearchLogSinkProvider.KindKey.Should().Be("opensearch");
    }
}
