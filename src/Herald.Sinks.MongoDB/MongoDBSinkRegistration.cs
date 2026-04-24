// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
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
