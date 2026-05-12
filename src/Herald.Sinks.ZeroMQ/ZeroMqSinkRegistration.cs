// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
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
