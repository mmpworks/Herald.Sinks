// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.ZeroMQ.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.ZeroMQ;

public static class ZeroMqSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new ZeroMqLogSinkProvider());
    }
}
