// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Herald.Sinks.Debug;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Services;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Debug.Tests;

/// <summary>
/// Tests the Debug sink by registering a custom TraceListener on
/// <see cref="Trace.Listeners"/> (which is the same collection
/// <see cref="Debug.WriteLine"/> fans out to) and asserting the
/// captured output.
/// </summary>
public sealed class DebugLogSinkTests : IDisposable
{
    private readonly CapturingTraceListener _listener;

    public DebugLogSinkTests()
    {
        _listener = new CapturingTraceListener();
        Trace.Listeners.Add(_listener);
    }

    public void Dispose()
    {
        Trace.Listeners.Remove(_listener);
        _listener.Dispose();
    }

    [Fact]
    public void Log_writes_formatted_line_with_level_and_category()
    {
        var sink = new DebugLogSink();
        var evt = LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Warn)
            .WithMessage("user {Name} signed in", "user Alice signed in")
            .Build();

        sink.Log(evt);

        _listener.LastLine.Should().Contain("[warn]");
        _listener.LastLine.Should().Contain("user Alice signed in");
    }

    [Fact]
    public void Log_includes_timestamp_in_hh_mm_ss_fff_format()
    {
        var sink = new DebugLogSink();
        var evt = LogEventBuilder.Create()
            .WithTime(new DateTimeOffset(2025, 1, 15, 13, 45, 6, 789, TimeSpan.Zero))
            .Build();

        sink.Log(evt);

        _listener.LastLine.Should().StartWith("[13:45:06.789]");
    }

    [Fact]
    public void Log_appends_exception_text_when_context_carries_exception()
    {
        var sink = new DebugLogSink();
        var ex = new InvalidOperationException("boom");
        var evt = LogEventBuilder.Create()
            .WithMessage("operation failed")
            .WithContext(LogContextKeys.Exception, ex)
            .Build();

        sink.Log(evt);

        _listener.LastLine.Should().Contain("operation failed");
        _listener.LastLine.Should().Contain("InvalidOperationException");
        _listener.LastLine.Should().Contain("boom");
    }

    [Fact]
    public void Log_throws_on_null_event()
    {
        var sink = new DebugLogSink();
        Action act = () => sink.Log(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // Captures WriteLine output so the tests can assert against it.
    // Trace.Listeners is the same collection Debug.WriteLine fans out
    // to when DEBUG is defined in the calling assembly — which
    // Herald.Sinks.Debug always is, by csproj.
    private sealed class CapturingTraceListener : TraceListener
    {
        private readonly List<(string? Line, string? Category)> _entries = new();
        private readonly StringBuilder _pending = new();

        public string? LastLine => _entries.Count == 0 ? null : _entries[^1].Line;
        public string? LastCategory => _entries.Count == 0 ? null : _entries[^1].Category;

        public override void Write(string? message)
        {
            if (message is not null) _pending.Append(message);
        }

        public override void WriteLine(string? message)
        {
            _pending.Append(message ?? string.Empty);
            _entries.Add((_pending.ToString(), null));
            _pending.Clear();
        }

        public override void WriteLine(string? message, string? category)
        {
            _pending.Append(message ?? string.Empty);
            _entries.Add((_pending.ToString(), category));
            _pending.Clear();
        }
    }
}
