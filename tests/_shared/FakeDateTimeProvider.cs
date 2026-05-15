// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using MMP.Herald.Time;

namespace MMP.Herald.Tests.Helpers;

public sealed class FakeDateTimeProvider : IDateTimeProvider
{
    private DateTimeOffset _now = new(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);

    public DateTimeOffset GetUtcNow() => _now;

    public void Set(DateTimeOffset value) => _now = value;

    public void Advance(TimeSpan duration) => _now = _now.Add(duration);
}
