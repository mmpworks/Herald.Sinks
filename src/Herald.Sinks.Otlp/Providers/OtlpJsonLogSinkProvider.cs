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

namespace Herald.Sinks.Otlp.Providers;

/// <summary>
/// Sink provider for OpenTelemetry Protocol (OTLP) JSON export.
/// Posts log events to an OTEL collector endpoint.
/// </summary>
public sealed class OtlpJsonLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "otlp_json";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);
        return new OtlpJsonLogSink(definition.Uri, levelRegistry);
    }
}
