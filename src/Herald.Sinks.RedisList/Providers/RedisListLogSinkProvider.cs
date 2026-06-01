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

namespace Herald.Sinks.RedisList.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="RedisListLogSink"/> from a
/// <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><c>Uri</c> → Redis connection string (required).</item>
///   <item><c>Host</c> → list key (default <c>herald-logs</c>).</item>
/// </list>
/// </remarks>
public sealed class RedisListLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "redis_list";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);

        var listKey = string.IsNullOrWhiteSpace(definition.Host) ? "herald-logs" : definition.Host;
        var sink = new RedisListLogSink(definition.Uri, listKey);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }
}
