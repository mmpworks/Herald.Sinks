// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using MMP.Herald;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Levels;
using MMP.Herald.Output.Rendering;
using MMP.Herald.Pipeline;
using MMP.Herald.Routing;

namespace Herald.Sinks.UdpJsonLine.Providers;

/// <summary>
/// Sink provider for UDP JSON-line delivery. Mirrors the TCP provider's
/// shape; callers give host + port, receive a fire-and-forget datagram sink.
/// </summary>
public sealed class UdpJsonLineSinkProvider : ILogSinkProvider
{
    /// <summary>
    /// The sink-kind string that identifies this provider in JSON config.
    /// </summary>
    public const string KindKey = "udp_json_line";

    public string SinkKind => KindKey;
    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Host);

        if (definition.Port is null)
        {
            throw new InvalidOperationException(
                $"Sink '{definition.Name}' requires a numeric port for kind '{definition.Kind}'.");
        }

        return new UdpJsonLineLogSink(definition.Host, definition.Port.Value, levelRegistry);
    }
}
