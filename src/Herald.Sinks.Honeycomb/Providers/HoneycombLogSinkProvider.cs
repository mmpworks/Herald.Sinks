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

namespace Herald.Sinks.Honeycomb.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="HoneycombLogSink"/> from
/// a <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// Wire-up conventions:
/// <list type="bullet">
///   <item><c>Alias</c> holds the Honeycomb API key (X-Honeycomb-Team).</item>
///   <item><c>Host</c> holds the dataset name.</item>
///   <item><c>Uri</c>, when set, overrides the default endpoint host — used
///   to target Refinery proxies or self-hosted Honeycomb Enterprise.</item>
/// </list>
/// </remarks>
public sealed class HoneycombLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "honeycomb";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Alias);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Host);

        var sink = new HoneycombLogSink(
            apiKey: definition.Alias,
            dataset: definition.Host,
            endpoint: definition.Uri);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }
}
