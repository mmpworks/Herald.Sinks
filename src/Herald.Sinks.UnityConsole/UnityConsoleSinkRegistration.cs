// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.UnityConsole.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.UnityConsole;

/// <summary>
/// Opt-in registration helper for the Unity console sink.
/// </summary>
public static class UnityConsoleSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new UnityConsoleLogSinkProvider());
    }
}
