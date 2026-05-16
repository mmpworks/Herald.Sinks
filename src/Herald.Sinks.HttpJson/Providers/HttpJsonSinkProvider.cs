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

namespace Herald.Sinks.HttpJson.Providers;

/// <summary>
/// Sink provider for HTTP JSON (NDJSON) delivery.
/// </summary>
public sealed class HttpJsonSinkProvider : ILogSinkProvider
{
    /// <summary>
    /// The sink-kind string that identifies this provider in JSON config.
    /// Carried on the provider so the identifier travels with the sink.
    /// </summary>
    public const string KindKey = "http_json";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);
        return new HttpJsonLogSink(definition.Uri, levelRegistry);
    }
}
