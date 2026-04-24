// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using FluentAssertions;
using Herald.Sinks.Otlp;
using MMP.Herald.Levels;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Otlp.Tests;

public sealed class OtlpProtobufLogSinkTests
{
    private static readonly TimeSpan ExpectedTimeout = TimeSpan.FromSeconds(30);
    private readonly ILogLevelRegistry _registry = RegistryHelper.CreateDefault();

    [Fact]
    public void Log_sends_protobuf_payload()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        var sink = new OtlpProtobufLogSink(
            "http://localhost:4318/v1/logs",
            _registry,
            httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Info)
            .WithMessage("Protobuf test")
            .Build();

        sink.Log(evt);

        handler.RequestCount.Should().Be(1);
        handler.LastRequestBodyBytes.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Protobuf_payload_is_binary()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        var sink = new OtlpProtobufLogSink(
            "http://localhost:4318/v1/logs",
            _registry,
            httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        handler.LastRequestBodyBytes.Should().NotBeNullOrEmpty();
        handler.LastRequestBodyBytes!.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void LogBatch_sends_all_events_in_single_request()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        var sink = new OtlpProtobufLogSink(
            "http://localhost:4318/v1/logs",
            _registry,
            httpClient: client);

        var events = new[]
        {
            LogEventBuilder.Create().WithMessage("Batch 1").Build(),
            LogEventBuilder.Create().WithMessage("Batch 2").Build()
        };

        sink.LogBatch(events);

        handler.RequestCount.Should().Be(1);
        handler.LastRequestBodyBytes.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Resource_attributes_are_serialized()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        var attrs = new Dictionary<string, string> { ["service.name"] = "herald-tests" };
        var sink = new OtlpProtobufLogSink(
            "http://localhost:4318/v1/logs",
            _registry,
            resourceAttributes: attrs,
            httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        // Binary protobuf should contain the attribute string somewhere
        var bodyStr = System.Text.Encoding.UTF8.GetString(handler.LastRequestBodyBytes!);
        bodyStr.Should().Contain("herald-tests");
    }

    [Fact]
    public void Owned_client_has_30s_timeout()
    {
        using var sink = new OtlpProtobufLogSink("http://localhost:4318", _registry);

        var field = typeof(OtlpProtobufLogSink).GetField("_httpClient",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var client = (HttpClient)field.GetValue(sink)!;

        client.Timeout.Should().Be(ExpectedTimeout);
    }
}
