// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.Loki.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.Loki;

/// <summary>
/// Opt-in registration helper for consumers that want the Loki sink
/// without constructing the provider themselves.
/// </summary>
public static class LokiSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new LokiLogSinkProvider());
    }
}
