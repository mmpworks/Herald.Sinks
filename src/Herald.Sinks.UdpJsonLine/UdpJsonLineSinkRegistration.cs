// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.UdpJsonLine.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.UdpJsonLine;

/// <summary>
/// Opt-in registration helper for consumers that want the UDP JSON-line
/// sink without constructing the provider themselves.
/// </summary>
public static class UdpJsonLineSinkRegistration
{
    /// <summary>
    /// Register the UDP JSON-line sink provider on the given
    /// <see cref="LogSinkProviderRegistry"/>. Idempotent.
    /// </summary>
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new UdpJsonLineSinkProvider());
    }
}
