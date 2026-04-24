// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.InMemory.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.InMemory;

/// <summary>
/// Opt-in registration helper for the InMemory sink.
/// </summary>
public static class InMemorySinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new InMemoryLogSinkProvider());
    }
}
