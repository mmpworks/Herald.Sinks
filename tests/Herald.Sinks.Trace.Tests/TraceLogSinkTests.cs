// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Herald.Sinks.Trace;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Services;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Trace.Tests;

public sealed class TraceLogSinkTests : IDisposable
{
    private readonly CapturingTraceListener _listener;

    public TraceLogSinkTests()
    {
        _listener = new CapturingTraceListener();
        System.Diagnostics.Trace.Listeners.Add(_listener);
    }

    public void Dispose()
    {
        System.Diagnostics.Trace.Listeners.Remove(_listener);
        _listener.Dispose();
    }

    [Fact]
    public void Log_writes_formatted_line_with_level_and_category()
    {
        var sink = new TraceLogSink();
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
        var sink = new TraceLogSink();
        var evt = LogEventBuilder.Create()
            .WithTime(new DateTimeOffset(2025, 1, 15, 13, 45, 6, 789, TimeSpan.Zero))
            .Build();

        sink.Log(evt);

        _listener.LastLine.Should().StartWith("[13:45:06.789]");
    }

    [Fact]
    public void Log_appends_exception_text_when_context_carries_exception()
    {
        var sink = new TraceLogSink();
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
        var sink = new TraceLogSink();
        Action act = () => sink.Log(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    private sealed class CapturingTraceListener : TraceListener
    {
        private readonly List<string?> _lines = new();
        private readonly StringBuilder _pending = new();

        public string? LastLine => _lines.Count == 0 ? null : _lines[^1];

        public override void Write(string? message)
        {
            if (message is not null) _pending.Append(message);
        }

        public override void WriteLine(string? message)
        {
            _pending.Append(message ?? string.Empty);
            _lines.Add(_pending.ToString());
            _pending.Clear();
        }
    }
}
