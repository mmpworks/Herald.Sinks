// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using FluentAssertions;
using Herald.Sinks.MSSqlServer.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.MSSqlServer.Tests;

public sealed class MSSqlServerLogSinkProviderTests
{
    [Fact]
    public void SinkKind_equals_kind_key()
    {
        new MSSqlServerLogSinkProvider().SinkKind.Should().Be(MSSqlServerLogSinkProvider.KindKey);
        MSSqlServerLogSinkProvider.KindKey.Should().Be("mssql");
    }

    [Fact]
    public void Minimum_edition_is_community()
    {
        new MSSqlServerLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
