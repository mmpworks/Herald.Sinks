// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.Seq.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.Seq;

/// <summary>
/// Opt-in registration helper for consumers that want the Seq sink
/// without constructing the provider themselves.
/// </summary>
public static class SeqSinkRegistration
{
    /// <summary>
    /// Register the Seq sink provider on the given
    /// <see cref="LogSinkProviderRegistry"/>. Idempotent.
    /// </summary>
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new SeqLogSinkProvider());
    }
}
