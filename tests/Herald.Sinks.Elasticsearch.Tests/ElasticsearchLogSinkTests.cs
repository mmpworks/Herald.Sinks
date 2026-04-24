// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Net.Http;
using System.Reflection;
using FluentAssertions;
using Herald.Sinks.Elasticsearch;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Elasticsearch.Tests;

public sealed class ElasticsearchLogSinkTests
{
    private static readonly TimeSpan ExpectedTimeout = TimeSpan.FromSeconds(30);
    private readonly ILogLevelRegistry _registry = RegistryHelper.CreateDefault();

    [Fact]
    public void Null_base_url_throws()
    {
        var act = () => new ElasticsearchLogSink(null!, _registry);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Invalid_index_prefix_throws()
    {
        var act = () => new ElasticsearchLogSink("http://localhost:9200", _registry, indexPrefix: "-starts-with-hyphen");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Log_posts_bulk_ndjson_with_action_and_document()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new ElasticsearchLogSink("http://localhost:9200", _registry, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Warn)
            .WithMessage("payload", "payload")
            .Build();

        sink.Log(evt);

        handler.RequestCount.Should().Be(1);
        handler.Requests[0].RequestUri!.ToString().Should().Be("http://localhost:9200/_bulk");
        handler.Requests[0].Content!.Headers.ContentType!.MediaType.Should().Be("application/x-ndjson");

        var body = handler.LastRequestBodyString!;
        body.Should().Contain("\"_index\":\"herald-logs-");
        body.Should().Contain("\"message\":\"payload\"");
    }

    [Fact]
    public void Owned_client_has_30s_timeout()
    {
        using var sink = new ElasticsearchLogSink("http://localhost:9200", _registry);

        var field = typeof(ElasticsearchLogSink).GetField("_httpClient",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var client = (HttpClient)field.GetValue(sink)!;

        client.Timeout.Should().Be(ExpectedTimeout);
    }
}
