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

namespace Herald.Sinks.Parquet.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="ParquetLogSink"/> from a
/// <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// Wire-up:
/// <list type="bullet">
///   <item><c>Uri</c> → output directory (required). Created if missing.</item>
///   <item><c>Host</c> → file-name prefix, default <c>herald-logs</c>.</item>
/// </list>
/// The Iceberg catalog client is not configurable through the
/// runtime-definition surface — wire one in by constructing
/// <see cref="ParquetLogSink"/> directly and registering with
/// <c>WithCustomSinkProvider</c>.
/// </remarks>
public sealed class ParquetLogSinkProvider : BatchingSinkProviderBase
{
    public const string KindKey = "parquet";

    public override string SinkKind => KindKey;
    public override HeraldEdition MinimumEdition => HeraldEdition.Community;

    public override ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);

        var prefix = string.IsNullOrWhiteSpace(definition.Host) ? "herald-logs" : definition.Host;
        var sink = new ParquetLogSink(outputDirectory: definition.Uri, filePrefix: prefix);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }
}
