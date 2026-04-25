// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.BigQuery;
using Herald.Sinks.BigQuery.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.BigQuery.Tests;

public sealed class BigQueryLogSinkTests
{
    [Fact] public void Constructor_throws_on_null_client() =>
        ((Action)(() => new BigQueryLogSink(client: null!, datasetId: "d", tableId: "t"))).Should().Throw<ArgumentNullException>();

    [Fact] public void Provider_kind_and_edition()
    {
        new BigQueryLogSinkProvider().SinkKind.Should().Be("bigquery");
        new BigQueryLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
