// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using MMP.Herald;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Levels;
using MMP.Herald.Output.Rendering;
using MMP.Herald.Pipeline;
using MMP.Herald.Routing;

namespace Herald.Sinks.TcpJsonLine.Providers;

/// <summary>
/// Sink provider for TCP JSONL (newline-delimited JSON) delivery.
/// </summary>
public sealed class TcpJsonLineSinkProvider : ILogSinkProvider
{
    /// <summary>
    /// The sink-kind string that identifies this provider in JSON config.
    /// Carried on the provider so the identifier travels with the sink.
    /// </summary>
    public const string KindKey = "tcp_json_line";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

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

        return new TcpJsonLineLogSink(definition.Host, definition.Port.Value, levelRegistry);
    }
}
