// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.Coralogix;
using Herald.Sinks.Coralogix.Providers;
using MMP.Herald;
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
}
