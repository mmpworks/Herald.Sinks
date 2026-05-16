// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.Parquet.Providers;
using Xunit;

namespace Herald.Sinks.Parquet.Tests;

public sealed class ParquetLogSinkProviderTests
{
    [Fact] public void KindKey_constant_is_parquet() =>
        ParquetLogSinkProvider.KindKey.Should().Be("parquet");

    [Fact] public void Provider_SinkKind_matches_KindKey() =>
        new ParquetLogSinkProvider().SinkKind.Should().Be(ParquetLogSinkProvider.KindKey);

    [Fact] public void Provider_throws_on_null_definition() =>
        ((Action)(() => new ParquetLogSinkProvider().CreateSink(
            definition: null!, levelRegistry: null!, transformerRegistry: null!)))
            .Should().Throw<ArgumentNullException>();
}
