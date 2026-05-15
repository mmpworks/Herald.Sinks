// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Globalization;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Events;
using MMP.Herald.Services;
using Xunit.Abstractions;

namespace Herald.Sinks.XUnit;

/// <summary>
/// Sink that writes formatted log events to xUnit's
/// <see cref="ITestOutputHelper"/>. Each test gets a fresh helper
/// scoped to the test instance, so register the sink per-test rather
/// than via a shared pipeline.
/// </summary>
/// <remarks>
/// <para>
/// xUnit only accepts output while the test is running. Calls to
/// <see cref="ITestOutputHelper.WriteLine(string)"/> after the test
/// finishes throw <see cref="InvalidOperationException"/>. This sink
/// catches that exception and drops the write — a late log call from
/// a lingering background task should not fail a test that already
/// passed.
/// </para>
/// <para>
/// Format: <c>[HH:mm:ss.fff] [level] [category] message</c>, exception
/// text on a following line when present. Same shape as the Debug,
/// Trace, and TextWriter sinks in the parity pack so test output is
/// readable across harnesses.
/// </para>
/// </remarks>
public sealed class XUnitLogSink : HeraldSinkBase
{
    private readonly ITestOutputHelper _output;

    public XUnitLogSink(ITestOutputHelper output)
    {
        ArgumentNullException.ThrowIfNull(output);
        _output = output;
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        var line = FormatLine(logEvent);

        try
        {
            _output.WriteLine(line);
        }
        catch (InvalidOperationException)
        {
            // Test already completed. Drop the write rather than fail
            // the next test with a stray diagnostic exception. This
            // matches Serilog.Sinks.XUnit's behaviour.
        }
    }

    private static string FormatLine(LogEvent evt)
    {
        var time = evt.TimeUtc.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var level = evt.Level.Key;
        var category = evt.Category.Value;
        var message = string.IsNullOrEmpty(evt.Message) ? evt.MessageTemplate : evt.Message;

        var line = $"[{time}] [{level}] [{category}] {message}";

        if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value) && value is Exception ex)
        {
            line += Environment.NewLine + ex;
        }

        return line;
    }
}
