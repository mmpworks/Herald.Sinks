// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.Debug.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.Debug;

/// <summary>
/// Opt-in registration helper for consumers that want the Debug sink
/// wired up without constructing the provider themselves. Mirrors
/// every other Herald.Sinks package's registration helper.
/// </summary>
public static class DebugSinkRegistration
{
    /// <summary>
    /// Register the Debug sink provider on the given
    /// <see cref="LogSinkProviderRegistry"/>. Idempotent — re-registering
    /// overwrites the existing entry in place.
    /// </summary>
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new DebugLogSinkProvider());
    }
}
