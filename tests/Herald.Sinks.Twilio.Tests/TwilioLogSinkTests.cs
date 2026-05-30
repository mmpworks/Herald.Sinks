// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.Twilio;
using Herald.Sinks.Twilio.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.Twilio.Tests;

public sealed class TwilioLogSinkTests
{
    [Fact] public void Constructor_throws_on_null_sid() =>
        ((Action)(() => new TwilioLogSink(accountSid: null!, "t", "+1", "+1"))).Should().Throw<ArgumentException>();

    [Fact] public void Constructor_throws_on_null_to() =>
        ((Action)(() => new TwilioLogSink("sid", "t", "+1", toNumber: null!))).Should().Throw<ArgumentException>();

    [Fact] public void Provider_throws_on_null_definition() =>
        ((Action)(() => new TwilioLogSinkProvider().CreateSink(definition: null!, levelRegistry: null!, transformerRegistry: null!))).Should().Throw<ArgumentNullException>();

    [Fact] public void Provider_kind_and_edition()
    {
        new TwilioLogSinkProvider().SinkKind.Should().Be("twilio");
        new TwilioLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
