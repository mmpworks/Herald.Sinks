// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using FluentAssertions;
using Herald.Sinks.UnityConsole;
using MMP.Herald.Levels;
using MMP.Herald.Services;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.UnityConsole.Tests;

/// <summary>
/// Verifies routing using simple Action mocks — same delegate shape
/// the consumer wires to UnityEngine.Debug.Log / LogWarning /
/// LogError in real use. No Unity runtime needed.
/// </summary>
public sealed class UnityConsoleLogSinkTests
{
    private readonly List<string> _log = new();
    private readonly List<string> _warn = new();
    private readonly List<string> _error = new();

    private UnityConsoleLogSink CreateSink(string? category = null) =>
        new(_log.Add, _warn.Add, _error.Add, category);

    [Fact]
    public void Constructor_throws_on_null_log_delegate()
    {
        Action act = () => _ = new UnityConsoleLogSink(null!, _ => { }, _ => { });
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_throws_on_null_warn_delegate()
    {
        Action act = () => _ = new UnityConsoleLogSink(_ => { }, null!, _ => { });
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_throws_on_null_error_delegate()
    {
        Action act = () => _ = new UnityConsoleLogSink(_ => { }, _ => { }, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Info_level_routes_to_log()
    {
        var sink = CreateSink();
        sink.Log(LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Info)
            .WithMessage("hello unity")
            .Build());

        _log.Should().ContainSingle().Which.Should().Contain("hello unity");
        _warn.Should().BeEmpty();
        _error.Should().BeEmpty();
    }

    [Fact]
    public void Debug_level_routes_to_log()
    {
        var sink = CreateSink();
        sink.Log(LogEventBuilder.Create().WithLevel(KnownLogLevels.Debug).Build());

        _log.Should().ContainSingle();
        _warn.Should().BeEmpty();
        _error.Should().BeEmpty();
    }

    [Fact]
    public void Warn_level_routes_to_warn()
    {
        var sink = CreateSink();
        sink.Log(LogEventBuilder.Create().WithLevel(KnownLogLevels.Warn).Build());

        _log.Should().BeEmpty();
        _warn.Should().ContainSingle();
        _error.Should().BeEmpty();
    }

    [Fact]
    public void Error_level_routes_to_error()
    {
        var sink = CreateSink();
        sink.Log(LogEventBuilder.Create().WithLevel(KnownLogLevels.Error).Build());

        _log.Should().BeEmpty();
        _warn.Should().BeEmpty();
        _error.Should().ContainSingle();
    }

    [Fact]
    public void Critical_level_routes_to_error()
    {
        var sink = CreateSink();
        sink.Log(LogEventBuilder.Create().WithLevel(KnownLogLevels.Critical).Build());

        _error.Should().ContainSingle();
    }

    [Fact]
    public void Category_prefix_is_prepended_when_supplied()
    {
        var sink = CreateSink(category: "GameLoop");
        sink.Log(LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Info)
            .WithMessage("frame done")
            .Build());

        _log.Should().ContainSingle().Which.Should().StartWith("[GameLoop] ");
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
