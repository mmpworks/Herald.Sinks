// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.RavenDB;
using Herald.Sinks.RavenDB.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.RavenDB.Tests;

public sealed class RavenDBLogSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_urls()
    {
        Action act = () => new RavenDBLogSink(urls: null!, database: "logs");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_urls()
    {
        Action act = () => new RavenDBLogSink(urls: Array.Empty<string>(), database: "logs");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_database()
    {
        Action act = () => new RavenDBLogSink(urls: new[] { "http://localhost:8080" }, database: "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_store()
    {
        Action act = () => new RavenDBLogSink(store: null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Provider_sink_kind_is_ravendb()
    {
        new RavenDBLogSinkProvider().SinkKind.Should().Be("ravendb");
        RavenDBLogSinkProvider.KindKey.Should().Be("ravendb");
    }

    [Fact]
    public void Provider_is_community_edition()
    {
        new RavenDBLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
