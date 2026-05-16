// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.Cassandra;
using Herald.Sinks.Cassandra.Providers;
using Xunit;

namespace Herald.Sinks.Cassandra.Tests;

public sealed class CassandraLogSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_contact_points()
    {
        Action act = () => new CassandraLogSink(contactPoints: null!, keyspace: "herald");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_contact_points()
    {
        Action act = () => new CassandraLogSink(contactPoints: Array.Empty<string>(), keyspace: "herald");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_keyspace()
    {
        Action act = () => new CassandraLogSink(contactPoints: new[] { "localhost" }, keyspace: "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_null_session()
    {
        Action act = () => new CassandraLogSink(session: null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Provider_sink_kind_is_cassandra()
    {
        new CassandraLogSinkProvider().SinkKind.Should().Be("cassandra");
        CassandraLogSinkProvider.KindKey.Should().Be("cassandra");
    }

}
