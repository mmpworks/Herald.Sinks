// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.Syslog.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.Syslog;

/// <summary>
/// Opt-in registration helper for the Syslog sink.
/// </summary>
public static class SyslogSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new SyslogSinkProvider());
    }
}
