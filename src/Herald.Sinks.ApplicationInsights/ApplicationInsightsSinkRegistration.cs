// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.ApplicationInsights.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.ApplicationInsights;

public static class ApplicationInsightsSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new ApplicationInsightsLogSinkProvider());
    }
}
