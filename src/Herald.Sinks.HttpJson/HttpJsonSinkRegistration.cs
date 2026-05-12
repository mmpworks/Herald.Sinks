// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.HttpJson.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.HttpJson;

/// <summary>
/// Opt-in registration helper for consumers that want the HTTP JSON sink
/// without constructing the provider themselves. Core no longer registers
/// this provider by default — consumers opt in by referencing the NuGet
/// and calling <see cref="RegisterAll"/> on the sink-provider registry.
/// </summary>
public static class HttpJsonSinkRegistration
{
    /// <summary>
    /// Register the HTTP JSON sink provider on the given
    /// <see cref="LogSinkProviderRegistry"/>. Idempotent —
    /// double-registration overwrites the existing entry in place, so
    /// calling this more than once is safe.
    /// </summary>
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new HttpJsonSinkProvider());
    }
}
