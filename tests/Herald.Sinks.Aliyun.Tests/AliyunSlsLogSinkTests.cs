// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Herald.Sinks.Aliyun;
using Herald.Sinks.Aliyun.Providers;
using MMP.Herald;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Aliyun.Tests;

public sealed class AliyunSlsLogSinkTests
{
    private const string Endpoint = "https://cn-hangzhou.log.aliyuncs.com";
    private const string Project = "my-project";
    private const string Logstore = "my-logstore";
    private const string KeyId = "LTAI-STUB";
    private const string KeySecret = "STUB-SECRET";

    [Fact]
    public void Constructor_throws_on_null_endpoint()
    {
        Action act = () => new AliyunSlsLogSink(null!, Project, Logstore, KeyId, KeySecret);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_project()
    {
        Action act = () => new AliyunSlsLogSink(Endpoint, null!, Logstore, KeyId, KeySecret);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_logstore()
    {
        Action act = () => new AliyunSlsLogSink(Endpoint, Project, null!, KeyId, KeySecret);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Log_posts_signed_request_to_project_scoped_url()
    {
        var handler = new TestHttpMessageHandler();
        handler.RespondWith(HttpStatusCode.OK);
        var client = new HttpClient(handler);
        using var sink = new AliyunSlsLogSink(Endpoint, Project, Logstore, KeyId, KeySecret, client);

        sink.Log(LogEventBuilder.Create().WithMessage("hi").Build());

        handler.RequestCount.Should().Be(1);
        var req = handler.Requests[0];
        req.Method.Should().Be(HttpMethod.Post);
        req.RequestUri!.Host.Should().Be($"{Project}.cn-hangzhou.log.aliyuncs.com");
        req.RequestUri.AbsolutePath.Should().Be($"/logstores/{Logstore}/shards/lb");
        req.Headers.GetValues("Authorization").Single().Should().StartWith($"LOG {KeyId}:");
        req.Headers.GetValues("x-log-apiversion").Single().Should().Be("0.6.0");
        req.Headers.GetValues("x-log-signaturemethod").Single().Should().Be("hmac-sha1");

        using var doc = JsonDocument.Parse(handler.LastRequestBodyString!);
        doc.RootElement.GetProperty("__logs__").GetArrayLength().Should().Be(1);
        var first = doc.RootElement.GetProperty("__logs__")[0];
        first.GetProperty("message").GetString().Should().Be("hi");
    }

    [Fact]
    public void Log_skips_signing_when_credentials_empty_for_custom_auth_handler_scenarios()
    {
        var handler = new TestHttpMessageHandler();
        handler.RespondWith(HttpStatusCode.OK);
        var client = new HttpClient(handler);
        using var sink = new AliyunSlsLogSink(Endpoint, Project, Logstore, "", "", client);

        sink.Log(LogEventBuilder.Create().Build());

        handler.Requests[0].Headers.Contains("Authorization").Should().BeFalse();
    }

    [Fact]
    public void Provider_sink_kind_is_aliyun_sls()
    {
        new AliyunSlsLogSinkProvider().SinkKind.Should().Be("aliyun_sls");
        AliyunSlsLogSinkProvider.KindKey.Should().Be("aliyun_sls");
    }

    [Fact]
    public void Provider_is_community_edition()
    {
        new AliyunSlsLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
