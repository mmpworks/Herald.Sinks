// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.GoogleCloudLogging.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.GoogleCloudLogging;

/// <summary>
/// Opt-in registration helper for the GoogleCloudLogging sink.
/// </summary>
public static class GoogleCloudLoggingSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new GoogleCloudLoggingSinkProvider());
    }
}
