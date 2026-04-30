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
/// Sink provider for protobuf file output.
/// Writes length-delimited OTLP protobuf records to .pb files.
/// </summary>
public sealed class ProtobufFileLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "protobuf_file";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Path);

        var maxSize = definition.RollingPolicy?.MaxBytes ?? 0;

        return new ProtobufFileLogSink(
            definition.Path,
            levelRegistry,
            maxFileSizeBytes: maxSize);
    }
}
