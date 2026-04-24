// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Herald.Sinks.Honeycomb;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Services;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Honeycomb.Tests;

public sealed class HoneycombLogSinkTests
{
    private const string ApiKey = "hc-test-key";
    private const string Dataset = "herald-test";

    [Fact]
    public void Log_posts_batch_array_with_data_object()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new HoneycombLogSink(ApiKey, Dataset, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Warn)
            .WithMessage("Low disk on {host}", "Low disk on nodeA")
            .Build();

        sink.Log(evt);

        handler.RequestCount.Should().Be(1);
        var doc = JsonDocument.Parse(handler.LastRequestBodyString!);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().Should().Be(1);

        var entry = doc.RootElement[0];
        entry.GetProperty("time").GetString().Should().NotBeNullOrWhiteSpace();
        entry.GetProperty("samplerate").GetDouble().Should().Be(1.0);

        var data = entry.GetProperty("data");
        data.GetProperty("message").GetString().Should().Be("Low disk on nodeA");
        data.GetProperty("messageTemplate").GetString().Should().Be("Low disk on {host}");
        data.GetProperty("level").GetString().Should().Be("warn");
        data.GetProperty("category").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Log_sets_honeycomb_team_header()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new HoneycombLogSink(ApiKey, Dataset, httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        var request = handler.Requests[0];
        request.Headers.GetValues("X-Honeycomb-Team").Should().ContainSingle().Which.Should().Be(ApiKey);
    }

    [Fact]
    public void Endpoint_defaults_to_public_batch_url_with_dataset()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new HoneycombLogSink(ApiKey, Dataset, httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        handler.Requests[0].RequestUri!.ToString()
            .Should().Be($"https://api.honeycomb.io/1/batch/{Dataset}");
    }

    [Fact]
    public void Endpoint_override_is_used_verbatim_when_full_path_supplied()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new HoneycombLogSink(
            ApiKey, Dataset,
            endpoint: "https://refinery.example.com:8080/1/batch/custom-dataset",
            httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        handler.Requests[0].RequestUri!.ToString()
            .Should().Be("https://refinery.example.com:8080/1/batch/custom-dataset");
    }

    [Fact]
    public void Properties_land_flat_inside_data_object()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new HoneycombLogSink(ApiKey, Dataset, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithProperty("UserId", 42)
            .WithProperty("Region", "eu-west-1")
            .Build();

        sink.Log(evt);

        var data = JsonDocument.Parse(handler.LastRequestBodyString!).RootElement[0].GetProperty("data");
        data.GetProperty("UserId").GetInt32().Should().Be(42);
        data.GetProperty("Region").GetString().Should().Be("eu-west-1");
    }

    [Fact]
    public void Exception_surfaces_as_flat_fields_on_data_object()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new HoneycombLogSink(ApiKey, Dataset, httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithContext(LogContextKeys.Exception, new InvalidOperationException("boom"))
            .Build();

        sink.Log(evt);

        var data = JsonDocument.Parse(handler.LastRequestBodyString!).RootElement[0].GetProperty("data");
        data.GetProperty("exception").GetString().Should().Contain("boom");
        data.GetProperty("exception.type").GetString().Should().Be("System.InvalidOperationException");
    }

    [Fact]
    public void Constructor_rejects_zero_or_negative_sample_rate()
    {
        var act = () => new HoneycombLogSink(ApiKey, Dataset, sampleRate: 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LogBatch_empty_is_a_no_op()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        using var sink = new HoneycombLogSink(ApiKey, Dataset, httpClient: client);

        sink.LogBatch(Array.Empty<LogEvent>());

        handler.RequestCount.Should().Be(0);
    }
}
