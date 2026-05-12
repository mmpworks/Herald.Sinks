// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.SignalFx.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.SignalFx;

/// <summary>
/// Opt-in registration helper for consumers that want the SignalFx sink
/// wired up without constructing the provider themselves. Parallels
/// <c>KafkaSinkRegistration</c> and the Azure / AWS sibling helpers —
/// Core does not know about sinks that live in this repo, so
/// registration is the consumer's responsibility.
/// </summary>
/// <example>
/// <code>
/// var registry = new LogSinkProviderRegistry();
/// SignalFxSinkRegistration.RegisterAll(registry);
/// // or via QuickLogBuilder:
/// builder.WithCustomSinkProvider(new SignalFxLogSinkProvider());
/// </code>
/// </example>
public static class SignalFxSinkRegistration
{
    /// <summary>
    /// Register the SignalFx sink provider on the given
    /// <see cref="LogSinkProviderRegistry"/>. Idempotent —
    /// double-registration overwrites the existing entry in place, so
    /// calling this more than once is safe.
    /// </summary>
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new SignalFxLogSinkProvider());
    }
}
