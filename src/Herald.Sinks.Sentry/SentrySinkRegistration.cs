// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.Sentry.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.Sentry;

/// <summary>
/// Opt-in registration helper for consumers that want the Sentry sink
/// without constructing the provider themselves.
/// </summary>
public static class SentrySinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new SentryLogSinkProvider());
    }
}
