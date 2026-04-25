// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.GodotConsole.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.GodotConsole;

/// <summary>
/// Opt-in registration helper for the Godot console sink.
/// </summary>
public static class GodotConsoleSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new GodotConsoleLogSinkProvider());
    }
}
