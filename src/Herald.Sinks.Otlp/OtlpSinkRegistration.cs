// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.Otlp.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.Otlp;

/// <summary>
/// Opt-in registration helper for consumers that want the OTLP sinks
/// (JSON, protobuf, protobuf-file) without constructing the three
/// providers themselves. Register the trio together, or pick-and-choose
/// via the individual Register* helpers below.
/// </summary>
public static class OtlpSinkRegistration
{
    /// <summary>
    /// Register all three OTLP providers: otlp_json, otlp_protobuf,
    /// and protobuf_file. Idempotent.
    /// </summary>
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new OtlpJsonLogSinkProvider());
        registry.Register(new OtlpProtobufLogSinkProvider());
        registry.Register(new ProtobufFileLogSinkProvider());
    }

    /// <summary>Register only the OTLP JSON sink provider.</summary>
    public static void RegisterJson(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new OtlpJsonLogSinkProvider());
    }

    /// <summary>Register only the OTLP protobuf sink provider.</summary>
    public static void RegisterProtobuf(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new OtlpProtobufLogSinkProvider());
    }

    /// <summary>Register only the protobuf-file sink provider.</summary>
    public static void RegisterProtobufFile(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new ProtobufFileLogSinkProvider());
    }
}
