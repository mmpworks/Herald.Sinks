// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.XUnit.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.XUnit;

/// <summary>
/// Opt-in registration helper. Registers the sink-kind so JSON
/// references to <c>xunit</c> fail with a clear message instead of
/// "unknown kind". The common path is to instantiate
/// <see cref="XUnitLogSink"/> directly in a test ctor and pass it
/// through <c>WithCustomSinkProvider</c>.
/// </summary>
public static class XUnitSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new XUnitLogSinkProvider());
    }
}
