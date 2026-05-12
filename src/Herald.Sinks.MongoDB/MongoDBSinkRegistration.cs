// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.MongoDB.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.MongoDB;

/// <summary>
/// Opt-in registration helper for the MongoDB sink.
/// </summary>
public static class MongoDBSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new MongoDBLogSinkProvider());
    }
}
