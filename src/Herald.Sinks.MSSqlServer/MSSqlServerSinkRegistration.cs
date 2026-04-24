// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.MSSqlServer.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.MSSqlServer;

/// <summary>
/// Opt-in registration helper for the MSSqlServer sink.
/// </summary>
public static class MSSqlServerSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new MSSqlServerLogSinkProvider());
    }
}
