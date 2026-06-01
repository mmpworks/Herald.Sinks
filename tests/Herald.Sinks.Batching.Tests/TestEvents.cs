// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using MMP.Herald.Events;
using MMP.Herald.Levels;

namespace Herald.Sinks.Batching.Tests;

/// <summary>
/// Minimal LogEvent factory local to the batching suite. Kept here rather
/// than reaching for the repo-shared LogEventBuilder so this project's
/// green/red state does not depend on the wider shared-helper state.
/// </summary>
internal static class TestEvents
{
    public static LogEvent Make(string message = "m") => new(
        TimeUtc: new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
        Level: KnownLogLevels.Information,
        Category: LogCategory.App,
        MessageTemplate: message,
        Message: message,
        Properties: LogEvent.EmptyProperties,
        Context: LogEvent.EmptyContext);
}
