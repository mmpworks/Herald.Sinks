// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Runtime.InteropServices;
using FluentAssertions;
using Herald.Sinks.EventLog;
using Xunit;

namespace Herald.Sinks.EventLog.Tests;

/// <summary>
/// Tests that exercise the sink's construction contract without
/// actually writing to the Windows Event Log. Writing requires a
/// registered source (admin-only) and a Windows target — neither
/// holds in CI. Functional tests for the write path live out of
/// tree in a manual-verification script.
/// </summary>
public sealed class EventLogSinkTests
{
    private static bool OnWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    [Fact]
    public void Constructor_throws_on_null_source()
    {
        Action act = () => new EventLogSink(source: null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_source()
    {
        Action act = () => new EventLogSink(source: "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_on_empty_log_name()
    {
        Action act = () => new EventLogSink(source: "Herald", logName: "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_throws_PlatformNotSupported_on_non_windows()
    {
        if (OnWindows)
        {
            // Test is only meaningful off-Windows. On Windows the ctor
            // succeeds (or fails for other reasons) — the platform
            // guard does not fire.
            return;
        }

        Action act = () => new EventLogSink(source: "Herald");
        act.Should().Throw<PlatformNotSupportedException>();
    }

    [Fact]
    public void Constructor_accepts_valid_args_on_windows()
    {
        if (!OnWindows)
        {
            return;
        }

        // Uses a predeclared source — "Application" is always present
        // on Windows. Does not call WriteEntry, so the lack of source
        // registration for a fresh name doesn't matter here.
        Action act = () => new EventLogSink(source: "Application");
        act.Should().NotThrow();
    }
}
