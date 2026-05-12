// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.Exceptionless.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.Exceptionless;

public static class ExceptionlessSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new ExceptionlessLogSinkProvider());
    }
}
