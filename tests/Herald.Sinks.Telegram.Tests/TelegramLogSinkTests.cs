// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.Telegram;
using Herald.Sinks.Telegram.Providers;
using MMP.Herald;
using Xunit;

namespace Herald.Sinks.Telegram.Tests;

public sealed class TelegramLogSinkTests
{
    [Fact] public void Constructor_throws_on_null_token() =>
        ((Action)(() => new TelegramLogSink(botToken: null!, chatId: "1"))).Should().Throw<ArgumentException>();

    [Fact] public void Constructor_throws_on_null_chat() =>
        ((Action)(() => new TelegramLogSink(botToken: "t", chatId: null!))).Should().Throw<ArgumentException>();

    [Fact] public void Provider_kind_and_edition()
    {
        new TelegramLogSinkProvider().SinkKind.Should().Be("telegram");
        new TelegramLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }
}
