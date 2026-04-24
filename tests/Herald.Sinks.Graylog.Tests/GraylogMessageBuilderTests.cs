// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Text.Json;
using FluentAssertions;
using Herald.Sinks.Graylog;
using Herald.Sinks.Graylog.Providers;
using MMP.Herald;
using MMP.Herald.Levels;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Graylog.Tests;

public sealed class GraylogMessageBuilderTests
{
    [Fact]
    public void Builds_gelf_1_1_envelope_with_required_fields()
    {
        var evt = LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Warn)
            .WithMessage("hi there")
            .Build();

        var gelf = GraylogMessageBuilder.Build(evt, host: "srv-01");

        using var doc = JsonDocument.Parse(gelf);
        doc.RootElement.GetProperty("version").GetString().Should().Be("1.1");
        doc.RootElement.GetProperty("host").GetString().Should().Be("srv-01");
        doc.RootElement.GetProperty("short_message").GetString().Should().Be("hi there");
        doc.RootElement.GetProperty("timestamp").GetDouble().Should().BeGreaterThan(0);
        doc.RootElement.GetProperty("level").GetInt32().Should().Be(4);  // warn
    }

    [Theory]
    [InlineData("trace", 7)]
    [InlineData("debug", 7)]
    [InlineData("info", 6)]
    [InlineData("notice", 5)]
    [InlineData("warn", 4)]
    [InlineData("error", 3)]
    [InlineData("critical", 2)]
    [InlineData("security", 1)]
    public void Severity_maps_from_known_log_level(string levelKey, int expectedSeverity)
    {
        var level = levelKey switch
        {
            "trace" => KnownLogLevels.Trace,
            "debug" => KnownLogLevels.Debug,
            "info" => KnownLogLevels.Info,
            "notice" => KnownLogLevels.Notice,
            "warn" => KnownLogLevels.Warn,
            "error" => KnownLogLevels.Error,
            "critical" => KnownLogLevels.Critical,
            "security" => KnownLogLevels.Security,
            _ => throw new InvalidOperationException(),
        };

        var evt = LogEventBuilder.Create().WithLevel(level).Build();
        var gelf = GraylogMessageBuilder.Build(evt, host: "h");
        using var doc = JsonDocument.Parse(gelf);
        doc.RootElement.GetProperty("level").GetInt32().Should().Be(expectedSeverity);
    }

    [Fact]
    public void Custom_properties_get_underscore_prefix_per_gelf_spec()
    {
        var evt = LogEventBuilder.Create()
            .WithProperty("UserId", 42)
            .WithProperty("TenantId", "acme")
            .Build();

        var gelf = GraylogMessageBuilder.Build(evt, host: "h");

        using var doc = JsonDocument.Parse(gelf);
        doc.RootElement.TryGetProperty("_UserId", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("_TenantId", out _).Should().BeTrue();
    }

    [Fact]
    public void Provider_sink_kind_is_graylog()
    {
        new GraylogLogSinkProvider().SinkKind.Should().Be("graylog");
        GraylogLogSinkProvider.KindKey.Should().Be("graylog");
    }
}
