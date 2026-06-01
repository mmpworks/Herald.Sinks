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
/// Sink provider for protobuf file output.
/// Writes length-delimited OTLP protobuf records to .pb files.
/// </summary>
public sealed class ProtobufFileLogSinkProvider : BatchingSinkProviderBase
{
    public const string KindKey = "protobuf_file";

    public override string SinkKind => KindKey;
    public override HeraldEdition MinimumEdition => HeraldEdition.Community;

    public override ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Path);

        var maxSize = definition.RollingPolicy?.MaxBytes ?? 0;

        var sink = new ProtobufFileLogSink(
            definition.Path,
            levelRegistry,
            maxFileSizeBytes: maxSize);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }
}
