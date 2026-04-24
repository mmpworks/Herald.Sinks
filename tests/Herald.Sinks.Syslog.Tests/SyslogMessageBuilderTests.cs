// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Text.RegularExpressions;
using FluentAssertions;
using Herald.Sinks.Syslog;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Syslog.Tests;

/// <summary>
/// Wire-format coverage for <see cref="SyslogMessageBuilder"/>. Pins
/// the byte shape so a collector-facing regression surfaces as a test
/// failure. Network IO is tested manually against a rsyslog container
/// — see <c>docs/syslog-manual-verify.md</c>.
/// </summary>
public sealed class SyslogMessageBuilderTests
{
    private static readonly DateTimeOffset FixedTime =
        new(2025, 4, 15, 13, 45, 6, 789, TimeSpan.Zero);

    [Fact]
    public void Rfc5424_emits_version_1_and_priority()
    {
        var evt = LogEventBuilder.Create().WithTime(FixedTime).WithLevel(KnownLogLevels.Info).Build();

        var line = SyslogMessageBuilder.Build(
            evt, SyslogFormat.Rfc5424, SyslogFacility.User,
            host: "h", appName: "a", processId: "42");

        // User (1) * 8 + Info severity (6) = 14
        line.Should().StartWith("<14>1 ");
    }

    [Fact]
    public void Rfc5424_includes_iso_timestamp_with_microseconds_and_offset()
    {
        var evt = LogEventBuilder.Create().WithTime(FixedTime).Build();

        var line = SyslogMessageBuilder.Build(
            evt, SyslogFormat.Rfc5424, SyslogFacility.User,
            host: "h", appName: "a", processId: "42");

        // "<PRI>1 2025-04-15T13:45:06.789000+00:00 h a 42 - - message"
        line.Should().Contain("2025-04-15T13:45:06.789000");
    }

    [Fact]
    public void Rfc5424_emits_nil_msgid_and_structured_data()
    {
        var evt = LogEventBuilder.Create().WithTime(FixedTime).WithMessage("hello").Build();

        var line = SyslogMessageBuilder.Build(
            evt, SyslogFormat.Rfc5424, SyslogFacility.User,
            host: "h", appName: "a", processId: "42");

        // Match "h a 42 - - hello" — two NILVALUE "-" tokens before the msg.
        line.Should().EndWith("h a 42 - - hello");
    }

    [Fact]
    public void Rfc3164_emits_bsd_timestamp_and_pid_bracket()
    {
        var evt = LogEventBuilder.Create().WithTime(FixedTime).WithMessage("hello").Build();

        var line = SyslogMessageBuilder.Build(
            evt, SyslogFormat.Rfc3164, SyslogFacility.User,
            host: "h", appName: "a", processId: "42");

        // "<14>Apr 15 13:45:06 h a[42]: hello"
        line.Should().Be("<14>Apr 15 13:45:06 h a[42]: hello");
    }

    [Fact]
    public void Rfc3164_pads_single_digit_day_with_space()
    {
        var fifthApril = new DateTimeOffset(2025, 4, 5, 1, 2, 3, TimeSpan.Zero);
        var evt = LogEventBuilder.Create().WithTime(fifthApril).WithMessage("x").Build();

        var line = SyslogMessageBuilder.Build(
            evt, SyslogFormat.Rfc3164, SyslogFacility.User,
            host: "h", appName: "a", processId: "1");

        line.Should().Contain("Apr  5 01:02:03");
    }

    [Theory]
    [InlineData("trace", 7)]
    [InlineData("debug", 7)]
    [InlineData("info", 6)]
    [InlineData("notice", 5)]
    [InlineData("warn", 4)]
    [InlineData("error", 3)]
    [InlineData("critical", 2)]
    [InlineData("security", 1)]
    public void Severity_maps_from_known_log_level(string levelKey, int expectedSeverity)
    {
        var level = levelKey switch
        {
            "trace" => KnownLogLevels.Trace,
            "debug" => KnownLogLevels.Debug,
            "info" => KnownLogLevels.Info,
            "notice" => KnownLogLevels.Notice,
            "warn" => KnownLogLevels.Warn,
            "error" => KnownLogLevels.Error,
            "critical" => KnownLogLevels.Critical,
            "security" => KnownLogLevels.Security,
            _ => throw new InvalidOperationException("unexpected level key"),
        };

        var evt = LogEventBuilder.Create().WithTime(FixedTime).WithLevel(level).Build();

        var line = SyslogMessageBuilder.Build(
            evt, SyslogFormat.Rfc5424, SyslogFacility.User,
            host: "h", appName: "a", processId: "1");

        // Priority = facility * 8 + severity → User (1) * 8 + expected.
        var expected = 1 * 8 + expectedSeverity;
        var match = Regex.Match(line, @"^<(?<pri>\d+)>");
        match.Success.Should().BeTrue();
        int.Parse(match.Groups["pri"].Value).Should().Be(expected);
    }

    [Fact]
    public void Facility_is_used_in_priority_calculation()
    {
        var evt = LogEventBuilder.Create().WithTime(FixedTime).WithLevel(KnownLogLevels.Info).Build();

        var local3 = SyslogMessageBuilder.Build(
            evt, SyslogFormat.Rfc5424, SyslogFacility.Local3,
            host: "h", appName: "a", processId: "1");

        // Local3 (19) * 8 + Info (6) = 158
        local3.Should().StartWith("<158>1 ");
    }

    [Fact]
    public void Host_and_app_with_whitespace_are_sanitized()
    {
        var evt = LogEventBuilder.Create().WithTime(FixedTime).WithMessage("x").Build();

        var line = SyslogMessageBuilder.Build(
            evt, SyslogFormat.Rfc5424, SyslogFacility.User,
            host: "my host", appName: "my app", processId: "1");

        line.Should().Contain("my-host my-app");
    }
}
