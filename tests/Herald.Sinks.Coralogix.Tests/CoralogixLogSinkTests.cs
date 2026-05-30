// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Herald.Sinks.Coralogix;
using Herald.Sinks.Coralogix.Providers;
using MMP.Herald;
using MMP.Herald.Levels;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Coralogix.Tests;

public sealed class CoralogixLogSinkTests
{
    [Fact] public void Constructor_throws_on_null_key() =>
        ((Action)(() => new CoralogixLogSink(privateKey: null!, applicationName: "a", subsystemName: "s"))).Should().Throw<ArgumentException>();

    [Fact] public void Constructor_throws_on_null_app() =>
        ((Action)(() => new CoralogixLogSink("k", applicationName: null!, subsystemName: "s"))).Should().Throw<ArgumentException>();

    [Fact] public void Provider_throws_on_null_definition() =>
        ((Action)(() => new CoralogixLogSinkProvider().CreateSink(definition: null!, levelRegistry: null!, transformerRegistry: null!))).Should().Throw<ArgumentNullException>();

    [Fact] public void Provider_kind_and_edition()
    {
        new CoralogixLogSinkProvider().SinkKind.Should().Be("coralogix");
        new CoralogixLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }

    // ── ADR-SINK-002: property preservation (default off) ────────────

    private static string CaptureFirstEntryText(bool preserveProperties)
    {
        var handler = new TestHttpMessageHandler();
        using var client = new HttpClient(handler);
        using var sink = new CoralogixLogSink("key", "app", "sub",
            httpClient: client, preserveProperties: preserveProperties);

        var evt = LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Info)
            .WithMessage("hello", "hello")
            .WithProperty("UserId", 42L)
            .WithProperty("Region", "eastus")
            .Build();

        sink.Log(evt);

        using var doc = JsonDocument.Parse(handler.LastRequestBodyString!);
        return doc.RootElement.GetProperty("logEntries")[0].GetProperty("text").GetString()!;
    }

    [Fact]
    public void Preservation_off_text_is_the_bare_message()
    {
        // Today's behaviour: text is the plain rendered message string.
        CaptureFirstEntryText(preserveProperties: false).Should().Be("hello");
    }

    [Fact]
    public void Preservation_on_text_is_json_with_type_preserved_properties()
    {
        var text = CaptureFirstEntryText(preserveProperties: true);

        // text is now a JSON object string Coralogix parses into facets.
        using var inner = JsonDocument.Parse(text);
        var root = inner.RootElement;

        root.GetProperty("message").GetString().Should().Be("hello");
        root.GetProperty("UserId").ValueKind.Should().Be(JsonValueKind.Number);
        root.GetProperty("UserId").GetInt64().Should().Be(42L);
        root.GetProperty("Region").GetString().Should().Be("eastus");
    }
}
