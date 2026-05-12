// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.Axiom;
using Herald.Sinks.Axiom.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.Axiom.Tests;

public sealed class AxiomLogSinkTests
{
    [Fact] public void Constructor_throws_on_null_token() =>
        ((Action)(() => new AxiomLogSink(apiToken: null!, dataset: "d"))).Should().Throw<ArgumentException>();

    [Fact] public void Constructor_throws_on_null_dataset() =>
        ((Action)(() => new AxiomLogSink(apiToken: "t", dataset: null!))).Should().Throw<ArgumentException>();

    [Fact] public void Provider_kind_and_edition()
    {
        new AxiomLogSinkProvider().SinkKind.Should().Be("axiom");
        new AxiomLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
