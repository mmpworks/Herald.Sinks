// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using FluentAssertions;
using Herald.Sinks.XUnit;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Services;
using MMP.Herald.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Herald.Sinks.XUnit.Tests;

public sealed class XUnitLogSinkTests
{
    [Fact]
    public void Log_writes_formatted_line_to_output_helper()
    {
        var output = new CapturingOutputHelper();
        var sink = new XUnitLogSink(output);

        var evt = LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Warn)
            .WithMessage("user {Name} signed in", "user Alice signed in")
            .Build();

        sink.Log(evt);

        output.Lines.Should().HaveCount(1);
        output.Lines[0].Should().Contain("[warn]");
        output.Lines[0].Should().Contain("user Alice signed in");
    }

    [Fact]
    public void Log_includes_timestamp_in_hh_mm_ss_fff_format()
    {
        var output = new CapturingOutputHelper();
        var sink = new XUnitLogSink(output);

        var evt = LogEventBuilder.Create()
            .WithTime(new DateTimeOffset(2025, 1, 15, 13, 45, 6, 789, TimeSpan.Zero))
            .Build();

        sink.Log(evt);

        output.Lines[0].Should().StartWith("[13:45:06.789]");
    }

    [Fact]
    public void Log_appends_exception_text_when_context_carries_exception()
    {
        var output = new CapturingOutputHelper();
        var sink = new XUnitLogSink(output);

        var ex = new InvalidOperationException("boom");
        var evt = LogEventBuilder.Create()
            .WithMessage("operation failed")
            .WithContext(LogContextKeys.Exception, ex)
            .Build();

        sink.Log(evt);

        var line = output.Lines[0];
        line.Should().Contain("operation failed");
        line.Should().Contain("InvalidOperationException");
        line.Should().Contain("boom");
    }

    [Fact]
    public void Log_swallows_post_test_InvalidOperationException()
    {
        var output = new ThrowingOutputHelper();
        var sink = new XUnitLogSink(output);

        Action act = () => sink.Log(LogEventBuilder.Create().Build());

        // No exception bubbles out — the sink drops the write so a late
        // background-task log call cannot fail a passed test.
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_throws_on_null_output()
    {
        Action act = () => new XUnitLogSink(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Log_throws_on_null_event()
    {
        var sink = new XUnitLogSink(new CapturingOutputHelper());
        Action act = () => sink.Log(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    private sealed class CapturingOutputHelper : ITestOutputHelper
    {
        public List<string> Lines { get; } = new();
        public string Output => string.Join(Environment.NewLine, Lines);
        public void Write(string message) { Lines.Add(message); }
        public void Write(string format, params object[] args) { Lines.Add(string.Format(format, args)); }
        public void WriteLine(string message) { Lines.Add(message); }
        public void WriteLine(string format, params object[] args) { Lines.Add(string.Format(format, args)); }
    }

    private sealed class ThrowingOutputHelper : ITestOutputHelper
    {
        public string Output => string.Empty;
        public void Write(string message) => throw new InvalidOperationException("test completed");
        public void Write(string format, params object[] args) => throw new InvalidOperationException("test completed");
        public void WriteLine(string message) => throw new InvalidOperationException("test completed");
        public void WriteLine(string format, params object[] args) => throw new InvalidOperationException("test completed");
    }
}
