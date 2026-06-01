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
using MMP.Herald.Sinks.Batching;

namespace Herald.Sinks.Otlp.Providers;

/// <summary>
/// Sink provider for OpenTelemetry Protocol (OTLP) protobuf export.
/// Posts log events as application/x-protobuf to an OTEL collector endpoint.
/// </summary>
public sealed class OtlpProtobufLogSinkProvider : BatchingSinkProviderBase
{
    public const string KindKey = "otlp_protobuf";

    public override string SinkKind => KindKey;
    public override HeraldEdition MinimumEdition => HeraldEdition.Community;

    public override ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);
        var sink = new OtlpProtobufLogSink(definition.Uri, levelRegistry);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }
}
