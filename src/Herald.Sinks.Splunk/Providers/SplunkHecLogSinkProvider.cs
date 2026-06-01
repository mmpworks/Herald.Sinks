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

namespace Herald.Sinks.Splunk.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="SplunkHecLogSink"/> from
/// a <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// Wire-up conventions:
/// <list type="bullet">
///   <item><c>Uri</c> holds either the full HEC URL or the server root.</item>
///   <item><c>Alias</c> doubles as the HEC auth token.</item>
///   <item><c>Host</c> is used as the Splunk <c>host</c> field when set.</item>
/// </list>
/// </remarks>
public sealed class SplunkHecLogSinkProvider : ILogSinkProvider
{
    /// <summary>
    /// The sink-kind string that identifies this provider in JSON config.
    /// </summary>
    public const string KindKey = "splunk_hec";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Alias);

        var sink = new SplunkHecLogSink(
            hecUrl: definition.Uri,
            token: definition.Alias,
            host: definition.Host);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }
}
