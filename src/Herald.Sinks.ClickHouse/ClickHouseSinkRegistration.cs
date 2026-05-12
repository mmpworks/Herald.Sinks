// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.ClickHouse.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.ClickHouse;

public static class ClickHouseSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new ClickHouseLogSinkProvider());
    }
}
