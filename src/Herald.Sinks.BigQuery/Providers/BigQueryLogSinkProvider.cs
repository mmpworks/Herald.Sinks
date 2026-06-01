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

namespace Herald.Sinks.BigQuery.Providers;

public sealed class BigQueryLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "bigquery";
    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Host);

        var (dataset, table) = ParseHost(definition.Host);
        var sink = new BigQueryLogSink(definition.Uri, dataset, table);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }

    private static (string Dataset, string Table) ParseHost(string host)
    {
        var dot = host.IndexOf('.');
        return dot < 0 ? (host, "herald_logs") : (host[..dot], host[(dot + 1)..]);
    }
}
