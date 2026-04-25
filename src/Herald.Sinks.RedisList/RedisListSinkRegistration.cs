// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.RedisList.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.RedisList;

/// <summary>
/// Opt-in registration helper for the Redis List sink.
/// </summary>
public static class RedisListSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new RedisListLogSinkProvider());
    }
}
