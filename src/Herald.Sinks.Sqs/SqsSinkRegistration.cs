// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.Sqs.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.Sqs;

public static class SqsSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new SqsLogSinkProvider());
    }
}
