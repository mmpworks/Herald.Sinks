// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.RabbitMQ.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.RabbitMQ;

/// <summary>
/// Opt-in registration helper for the RabbitMQ sink.
/// </summary>
public static class RabbitMQSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new RabbitMQLogSinkProvider());
    }
}
