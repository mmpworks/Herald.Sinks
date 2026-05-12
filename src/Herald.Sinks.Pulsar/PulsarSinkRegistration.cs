// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.Pulsar.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.Pulsar;

public static class PulsarSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new PulsarLogSinkProvider());
    }
}
