// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Herald.Sinks.Loki;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Services;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Loki.Tests;

public sealed class LokiLogSinkTests
{
    private const string Endpoint = "http://loki.example.com:3100";

    [Fact]
    public void Log_posts_streams_payload_with_label_set_and_values_pair()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new LokiLogSink(Endpoint, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Warning)
            .WithMessage("payload", "payload")
            .Build();

        sink.Log(evt);

        handler.RequestCount.Should().Be(1);
        var doc = JsonDocument.Parse(handler.LastRequestBodyString!);

        var streams = doc.RootElement.GetProperty("streams");
        streams.GetArrayLength().Should().Be(1);

        var stream = streams[0];
        var labels = stream.GetProperty("stream");
        labels.GetProperty("level").GetString().Should().Be("warning");
        labels.GetProperty("category").GetString().Should().NotBeNullOrWhiteSpace();

        var values = stream.GetProperty("values");
        values.GetArrayLength().Should().Be(1);
        var pair = values[0];
        pair.GetArrayLength().Should().Be(2);
        pair[0].GetString().Should().MatchRegex("^[0-9]+$");  // nanosecond timestamp string
        pair[1].GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Endpoint_appends_push_path_when_only_host_given()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new LokiLogSink(Endpoint, httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        handler.Requests[0].RequestUri!.ToString()
            .Should().Be("http://loki.example.com:3100/loki/api/v1/push");
    }

    [Fact]
    public void Endpoint_preserved_verbatim_when_full_push_path_supplied()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new LokiLogSink(
            "https://logs-prod.grafana.net/loki/api/v1/push",
            httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        handler.Requests[0].RequestUri!.ToString()
            .Should().Be("https://logs-prod.grafana.net/loki/api/v1/push");
    }

    [Fact]
    public void Static_labels_merge_with_per_event_labels()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        var staticLabels = new Dictionary<string, string>
        {
            ["app"] = "myapp",
            ["env"] = "prod",
        };
        using var sink = new LokiLogSink(Endpoint, staticLabels: staticLabels, httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        var labels = JsonDocument.Parse(handler.LastRequestBodyString!)
            .RootElement.GetProperty("streams")[0].GetProperty("stream");
        labels.GetProperty("app").GetString().Should().Be("myapp");
        labels.GetProperty("env").GetString().Should().Be("prod");
        labels.GetProperty("level").GetString().Should().NotBeNullOrWhiteSpace();
        labels.GetProperty("category").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Bearer_token_sets_authorization_header()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new LokiLogSink(Endpoint, bearerToken: "grafana-cloud-token", httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        var auth = handler.Requests[0].Headers.Authorization!;
        auth.Scheme.Should().Be("Bearer");
        auth.Parameter.Should().Be("grafana-cloud-token");
    }

    [Fact]
    public void Basic_auth_encodes_user_and_password()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new LokiLogSink(
            Endpoint,
            basicAuthUser: "user",
            basicAuthPassword: "pass",
            httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        var auth = handler.Requests[0].Headers.Authorization!;
        auth.Scheme.Should().Be("Basic");
        var expected = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("user:pass"));
        auth.Parameter.Should().Be(expected);
    }

    [Fact]
    public void Batch_with_different_labels_produces_multiple_streams()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new LokiLogSink(Endpoint, httpClient: client);

        var events = new[]
        {
            LogEventBuilder.Create().WithLevel(KnownLogLevels.Information).Build(),
            LogEventBuilder.Create().WithLevel(KnownLogLevels.Warning).Build(),
            LogEventBuilder.Create().WithLevel(KnownLogLevels.Information).Build(),
        };

        sink.LogBatch(events);

        var streams = JsonDocument.Parse(handler.LastRequestBodyString!)
            .RootElement.GetProperty("streams");
        streams.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public void Properties_go_into_log_line_json_not_labels()
    {
        // Label cardinality guard: properties must NOT be promoted to
        // labels. A userId becoming a label would explode the index.
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new LokiLogSink(Endpoint, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithProperty("UserId", 42)
            .WithProperty("Region", "us-east-1")
            .Build();

        sink.Log(evt);

        var stream = JsonDocument.Parse(handler.LastRequestBodyString!)
            .RootElement.GetProperty("streams")[0];
        var labels = stream.GetProperty("stream");
        labels.TryGetProperty("UserId", out _).Should().BeFalse(
            "properties must not become labels (cardinality guard)");
        labels.TryGetProperty("Region", out _).Should().BeFalse();

        var line = stream.GetProperty("values")[0][1].GetString()!;
        var lineDoc = JsonDocument.Parse(line);
        lineDoc.RootElement.GetProperty("UserId").GetInt32().Should().Be(42);
        lineDoc.RootElement.GetProperty("Region").GetString().Should().Be("us-east-1");
    }

    [Fact]
    public void LogBatch_empty_is_a_no_op()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new LokiLogSink(Endpoint, httpClient: client);

        sink.LogBatch(Array.Empty<LogEvent>());

        handler.RequestCount.Should().Be(0);
    }
}
