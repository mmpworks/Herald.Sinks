// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.AzureServiceBus.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.AzureServiceBus;

public static class AzureServiceBusSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new AzureServiceBusLogSinkProvider());
    }
}
