// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Herald.Sinks.AzureAnalytics;
using Herald.Sinks.AzureAnalytics.Providers;
using MMP.Herald;
using MMP.Herald.Levels;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.AzureAnalytics.Tests;

public sealed class AzureAnalyticsLogSinkTests
{
    // Base64 of 32 zero bytes — valid base64, obviously not a real workspace key.
    private const string StubKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
    private const string StubWorkspaceId = "00000000-0000-0000-0000-000000000000";

    [Fact]
    public void Constructor_throws_on_null_workspace_id()
    {
        Action act = () => new AzureAnalyticsLogSink(workspaceId: null!, workspaceKey: StubKey);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_workspace_key()
    {
        Action act = () => new AzureAnalyticsLogSink(workspaceId: StubWorkspaceId, workspaceKey: null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_non_base64_workspace_key()
    {
        Action act = () => new AzureAnalyticsLogSink(workspaceId: StubWorkspaceId, workspaceKey: "not-base64!");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Constructor_throws_on_invalid_log_type_name()
    {
        Action act = () => new AzureAnalyticsLogSink(
            workspaceId: StubWorkspaceId, workspaceKey: StubKey, logType: "invalid name with spaces");
        act.Should().Throw<ArgumentException>().WithMessage("*alphanumeric*");
    }

    [Fact]
    public void Constructor_accepts_valid_alphanumeric_log_type()
    {
        Action act = () => new AzureAnalyticsLogSink(
            workspaceId: StubWorkspaceId, workspaceKey: StubKey, logType: "MyApp123");
        act.Should().NotThrow();
    }

    [Fact]
    public void Log_posts_signed_json_array_to_data_collector_endpoint()
    {
        var handler = new TestHttpMessageHandler();
        handler.RespondWith(HttpStatusCode.OK);
        var client = new HttpClient(handler);
        using var sink = new AzureAnalyticsLogSink(
            workspaceId: StubWorkspaceId,
            workspaceKey: StubKey,
            logType: "HeraldLog",
            httpClient: client);

        sink.Log(LogEventBuilder.Create().WithLevel(KnownLogLevels.Warn).WithMessage("hi").Build());

        handler.RequestCount.Should().Be(1);
        var req = handler.Requests[0];
        req.Method.Should().Be(HttpMethod.Post);
        req.RequestUri!.Host.Should().StartWith(StubWorkspaceId);
        req.RequestUri.AbsolutePath.Should().Be("/api/logs");

        // Required headers for the Data Collector API.
        req.Headers.GetValues("Authorization").Single().Should().StartWith("SharedKey ");
        req.Headers.GetValues("Log-Type").Single().Should().Be("HeraldLog");
        req.Headers.GetValues("x-ms-date").Should().ContainSingle();
        req.Headers.GetValues("time-generated-field").Single().Should().Be("time_utc");

        var body = handler.LastRequestBodyString!;
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        var first = doc.RootElement[0];
        first.GetProperty("level").GetString().Should().Be("warn");
        first.GetProperty("message").GetString().Should().Be("hi");
    }

    [Fact]
    public void Provider_sink_kind_is_azure_analytics()
    {
        new AzureAnalyticsLogSinkProvider().SinkKind.Should().Be("azure_analytics");
        AzureAnalyticsLogSinkProvider.KindKey.Should().Be("azure_analytics");
    }

    [Fact]
    public void Provider_is_community_edition()
    {
        new AzureAnalyticsLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
