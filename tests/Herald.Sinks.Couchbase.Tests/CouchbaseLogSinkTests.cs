// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.Couchbase;
using Herald.Sinks.Couchbase.Providers;
using Xunit;

namespace Herald.Sinks.Couchbase.Tests;

public sealed class CouchbaseLogSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_collection()
    {
        Action act = () => new CouchbaseLogSink(collection: null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Provider_sink_kind_is_couchbase()
    {
        new CouchbaseLogSinkProvider().SinkKind.Should().Be("couchbase");
        CouchbaseLogSinkProvider.KindKey.Should().Be("couchbase");
    }

    [Fact]
    public void Provider_throws_on_create_sink()
    {
        var provider = new CouchbaseLogSinkProvider();
        Action act = () => provider.CreateSink(definition: null!, levelRegistry: null!, transformerRegistry: null!);
        act.Should().Throw<NotSupportedException>();
    }
}
