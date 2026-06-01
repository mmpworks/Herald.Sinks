// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using FluentAssertions;
using Herald.Sinks.GodotConsole;
using MMP.Herald.Levels;
using MMP.Herald.Services;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.GodotConsole.Tests;

/// <summary>
/// Verifies routing without standing up a Godot runtime — the
/// internal ctor lets tests inject mock dispatch delegates so the
/// per-level routing logic can be exercised in isolation.
/// </summary>
public sealed class GodotConsoleLogSinkTests
{
    private readonly List<string> _print = new();
    private readonly List<string> _warn = new();
    private readonly List<string> _error = new();

    private GodotConsoleLogSink CreateSink(string? category = null) =>
        new(_print.Add, _warn.Add, _error.Add, category);

    [Fact]
    public void Info_level_routes_to_print()
    {
        var sink = CreateSink();
        sink.Log(LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Information)
            .WithMessage("hello world")
            .Build());

        _print.Should().ContainSingle().Which.Should().Contain("hello world");
        _warn.Should().BeEmpty();
        _error.Should().BeEmpty();
    }

    [Fact]
    public void Debug_level_routes_to_print()
    {
        var sink = CreateSink();
        sink.Log(LogEventBuilder.Create().WithLevel(KnownLogLevels.Debug).Build());

        _print.Should().ContainSingle();
        _warn.Should().BeEmpty();
        _error.Should().BeEmpty();
    }

    [Fact]
    public void Warn_level_routes_to_warn()
    {
        var sink = CreateSink();
        sink.Log(LogEventBuilder.Create().WithLevel(KnownLogLevels.Warning).Build());

        _print.Should().BeEmpty();
        _warn.Should().ContainSingle();
        _error.Should().BeEmpty();
    }

    [Fact]
    public void Error_level_routes_to_error()
    {
        var sink = CreateSink();
        sink.Log(LogEventBuilder.Create().WithLevel(KnownLogLevels.Error).Build());

        _print.Should().BeEmpty();
        _warn.Should().BeEmpty();
        _error.Should().ContainSingle();
    }

    [Fact]
    public void Critical_level_routes_to_error()
    {
        var sink = CreateSink();
        sink.Log(LogEventBuilder.Create().WithLevel(KnownLogLevels.Fatal).Build());

        _error.Should().ContainSingle();
    }

    [Fact]
    public void Category_prefix_is_prepended_when_supplied()
    {
        var sink = CreateSink(category: "Combat");
        sink.Log(LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Information)
            .WithMessage("hit")
            .Build());

        _print.Should().ContainSingle().Which.Should().StartWith("[Combat] ");
    }

    [Fact]
    public void Exception_text_is_appended_after_the_line()
    {
        var sink = CreateSink();
        var ex = new InvalidOperationException("boom");
        sink.Log(LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Error)
            .WithMessage("operation failed")
            .WithContext(LogContextKeys.Exception, ex)
            .Build());

        var line = _error.Should().ContainSingle().Subject;
        line.Should().Contain("operation failed");
        line.Should().Contain("InvalidOperationException");
        line.Should().Contain("boom");
    }

    [Fact]
    public void Log_throws_on_null_event()
    {
        var sink = CreateSink();
        Action act = () => sink.Log(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
