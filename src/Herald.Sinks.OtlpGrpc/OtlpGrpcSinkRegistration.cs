// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.OtlpGrpc.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.OtlpGrpc;

/// <summary>
/// Opt-in registration helper for the OTLP/gRPC sink.
/// </summary>
public static class OtlpGrpcSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new OtlpGrpcLogSinkProvider());
    }
}
