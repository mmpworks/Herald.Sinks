// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Tests.Helpers;

namespace Herald.Sinks.Conformance.Tests.Harness;

/// <summary>
/// The .NET twin of the Go logcreator's <c>event.Canonical()</c>. One logical
/// event — "User 42 logged in from 10.0.0.1" — that every reserved-format
/// answer key in docs/log-formats-reference.md is built from.
///
/// <para>
/// The property TYPES are the point. <c>UserId</c> is a <see cref="long"/> 42,
/// not the string "42"; <c>IP</c> is a string. A sink that survives the
/// fidelity bar must carry <c>UserId</c> out as a JSON number wherever the
/// format reserves it as a number (Datadog usr.id, OTLP intValue) and as a
/// string only where the format's schema legitimately demands one (ECS
/// user.id). The asserter pins which is which per format.
/// </para>
///
/// <para>
/// The timestamp carries sub-millisecond (100ns-tick) precision so the F2
/// truncation defect surfaces: a sink that floors to milliseconds drops the
/// trailing ticks. The canonical doc samples only render to milliseconds, so
/// the per-format answer keys assert the millisecond form; the dedicated F2
/// precision tests assert the full-tick survival on the round-trip sinks.
/// </para>
/// </summary>
public static class CanonicalEvent
{
    /// <summary>
    /// The reference instant. The Datadog/ECS answer keys render this as
    /// <c>2026-05-25T14:32:01.123Z</c>; the trailing 4567 ticks past the
    /// millisecond are what an F2-truncating timestamp path loses.
    /// </summary>
    public static readonly DateTimeOffset Instant =
        new DateTimeOffset(2026, 5, 25, 14, 32, 1, 123, TimeSpan.Zero).AddTicks(4_567);

    public const long UserId = 42L;
    public const string Ip = "10.0.0.1";
    public const string Service = "herald-demo";
    public const string Host = "web-01";
    public const string Template = "User {UserId} logged in from {IP}";
    public const string Rendered = "User 42 logged in from 10.0.0.1";

    public static LogEvent Build() =>
        LogEventBuilder.Create()
            .WithLevel(KnownLogLevels.Information)
            .WithMessage(Template, Rendered)
            .WithTime(Instant)
            .WithProperty("UserId", UserId)
            .WithProperty("IP", Ip)
            .Build();
}
