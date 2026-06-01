// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.IO;
using FluentAssertions;
using Herald.Sinks.TextWriter;
using Herald.Sinks.TextWriter.Providers;
using MMP.Herald;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Services;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.TextWriter.Tests;

public sealed class TextWriterLogSinkTests
{
    [Fact]
    public void Log_writes_formatted_line_to_writer()
    {
        var writer = new StringWriter();
        using var sink = new TextWriterLogSink(writer);

        var evt = LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Warning)
            .WithMessage("user {Name} signed in", "user Alice signed in")
            .Build();

        sink.Log(evt);

        var output = writer.ToString();
        output.Should().Contain("[warning]");
        output.Should().Contain("user Alice signed in");
        output.Should().EndWith(Environment.NewLine);
    }

    [Fact]
    public void Log_includes_timestamp_in_hh_mm_ss_fff_format()
    {
        var writer = new StringWriter();
        using var sink = new TextWriterLogSink(writer);

        var evt = LogEventBuilder.Create()
            .WithTime(new DateTimeOffset(2025, 1, 15, 13, 45, 6, 789, TimeSpan.Zero))
            .Build();

        sink.Log(evt);

        writer.ToString().Should().StartWith("[13:45:06.789]");
    }

    [Fact]
    public void Log_appends_exception_text_when_context_carries_exception()
    {
        var writer = new StringWriter();
        using var sink = new TextWriterLogSink(writer);

        var ex = new InvalidOperationException("boom");
        var evt = LogEventBuilder.Create()
            .WithMessage("operation failed")
            .WithContext(LogContextKeys.Exception, ex)
            .Build();

        sink.Log(evt);

        var output = writer.ToString();
        output.Should().Contain("operation failed");
        output.Should().Contain("InvalidOperationException");
        output.Should().Contain("boom");
    }

    [Fact]
    public void Flush_happens_after_every_event()
    {
        var writer = new FlushCountingWriter();
        using var sink = new TextWriterLogSink(writer);

        sink.Log(LogEventBuilder.Create().Build());
        sink.Log(LogEventBuilder.Create().Build());

        writer.FlushCount.Should().Be(2);
    }

    [Fact]
    public void Dispose_disposes_writer_when_ownership_requested()
    {
        var writer = new DisposeTrackingWriter();
        var sink = new TextWriterLogSink(writer, disposeWriter: true);

        sink.Dispose();

        writer.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public void Dispose_leaves_writer_alive_by_default()
    {
        var writer = new DisposeTrackingWriter();
        var sink = new TextWriterLogSink(writer);

        sink.Dispose();

        writer.WasDisposed.Should().BeFalse();
    }

    [Fact]
    public void Constructor_throws_on_null_writer()
    {
        Action act = () => new TextWriterLogSink(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Log_throws_on_null_event()
    {
        using var sink = new TextWriterLogSink(new StringWriter());
        Action act = () => sink.Log(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact] public void Provider_throws_on_create_sink() =>
        ((Action)(() => new TextWriterLogSinkProvider().CreateSink(
            definition: null!, levelRegistry: null!, transformerRegistry: null!)))
            .Should().Throw<NotSupportedException>();

    [Fact] public void Provider_kind_and_edition()
    {
        new TextWriterLogSinkProvider().SinkKind.Should().Be("text_writer");
        new TextWriterLogSinkProvider().MinimumEdition.Should().Be(HeraldEdition.Community);
    }

    private sealed class FlushCountingWriter : StringWriter
    {
        public int FlushCount { get; private set; }
        public override void Flush() { FlushCount++; base.Flush(); }
    }

    private sealed class DisposeTrackingWriter : StringWriter
    {
        public bool WasDisposed { get; private set; }
        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }
}
