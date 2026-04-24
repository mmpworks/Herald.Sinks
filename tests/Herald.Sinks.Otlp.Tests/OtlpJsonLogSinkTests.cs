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

public sealed class OtlpJsonLogSinkTests
{
    private static readonly TimeSpan ExpectedTimeout = TimeSpan.FromSeconds(30);
    private readonly ILogLevelRegistry _registry = RegistryHelper.CreateDefault();

    [Fact]
    public void Log_sends_otlp_json_payload()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        var sink = new OtlpJsonLogSink(
            "http://localhost:4318/v1/logs",
            _registry,
            httpClient: client);

        var evt = LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Info)
            .WithMessage("OTLP test")
            .Build();

        sink.Log(evt);

        handler.RequestCount.Should().Be(1);
        handler.LastRequestBodyString.Should().Contain("OTLP test");
    }

    [Fact]
    public void Resource_attributes_are_included()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        var attrs = new Dictionary<string, string> { ["service.name"] = "test-svc" };
        var sink = new OtlpJsonLogSink(
            "http://localhost:4318/v1/logs",
            _registry,
            resourceAttributes: attrs,
            httpClient: client);

        sink.Log(LogEventBuilder.Create().Build());

        handler.LastRequestBodyString.Should().Contain("test-svc");
    }

    [Fact]
    public void LogBatch_sends_multiple_events()
    {
        var handler = new TestHttpMessageHandler();
        var client = new HttpClient(handler);
        var sink = new OtlpJsonLogSink(
            "http://localhost:4318/v1/logs",
            _registry,
            httpClient: client);

        var events = new[]
        {
            LogEventBuilder.Create().WithMessage("Batch 1").Build(),
            LogEventBuilder.Create().WithMessage("Batch 2").Build(),
            LogEventBuilder.Create().WithMessage("Batch 3").Build()
        };

        sink.LogBatch(events);

        handler.RequestCount.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public void Owned_client_has_30s_timeout()
    {
        using var sink = new OtlpJsonLogSink("http://localhost:4318", _registry);

        var field = typeof(OtlpJsonLogSink).GetField("_httpClient",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var client = (HttpClient)field.GetValue(sink)!;

        client.Timeout.Should().Be(ExpectedTimeout);
    }
}
