// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.Mezmo;
using Herald.Sinks.Mezmo.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.Mezmo.Tests;

public sealed class MezmoLogSinkTests
{
    [Fact] public void Constructor_throws_on_null_key() =>
        ((Action)(() => new MezmoLogSink(ingestKey: null!, hostname: "h"))).Should().Throw<ArgumentException>();

    [Fact] public void Constructor_throws_on_null_hostname() =>
        ((Action)(() => new MezmoLogSink(ingestKey: "k", hostname: null!))).Should().Throw<ArgumentException>();

    [Fact] public void Provider_kind_and_edition()
    {
        new MezmoLogSinkProvider().SinkKind.Should().Be("mezmo");
        new MezmoLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
