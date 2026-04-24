// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.Trace.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.Trace;

/// <summary>
/// Opt-in registration helper for the Trace sink.
/// </summary>
public static class TraceSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new TraceLogSinkProvider());
    }
}
