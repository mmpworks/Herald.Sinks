// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.RavenDB.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.RavenDB;

public static class RavenDBSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new RavenDBLogSinkProvider());
    }
}
