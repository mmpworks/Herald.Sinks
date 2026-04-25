// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.Coralogix.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.Coralogix;

public static class CoralogixSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new CoralogixLogSinkProvider());
    }
}
