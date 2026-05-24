// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.Email;
using Herald.Sinks.Email.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.Email.Tests;

public sealed class EmailLogSinkTests
{
    [Fact]
    public void Constructor_throws_on_null_smtp_host()
    {
        Action act = () => new EmailLogSink(null!, 587, "from@example.com", new[] { "to@example.com" });
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_invalid_port()
    {
        Action act = () => new EmailLogSink("smtp.example.com", 0, "from@example.com", new[] { "to@example.com" });
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_throws_on_null_from_address()
    {
        Action act = () => new EmailLogSink("smtp.example.com", 587, null!, new[] { "to@example.com" });
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_recipient_list()
    {
        Action act = () => new EmailLogSink("smtp.example.com", 587, "from@example.com", Array.Empty<string>());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_accepts_valid_args()
    {
        Action act = () => new EmailLogSink("smtp.example.com", 587, "from@example.com", new[] { "to@example.com" });
        act.Should().NotThrow();
    }

    [Fact]
    public void Provider_throws_on_null_definition()
    {
        var provider = new EmailLogSinkProvider();
        Action act = () => provider.CreateSink(null!, null!, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Provider_sink_kind_is_email()
    {
        new EmailLogSinkProvider().SinkKind.Should().Be("email");
        EmailLogSinkProvider.KindKey.Should().Be("email");
    }
}
