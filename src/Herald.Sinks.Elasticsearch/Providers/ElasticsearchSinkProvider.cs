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

namespace Herald.Sinks.Elasticsearch.Providers;

/// <summary>
/// Sink provider for Elasticsearch via the Bulk API.
/// </summary>
public sealed class ElasticsearchSinkProvider : ILogSinkProvider
{
    public const string KindKey = "elasticsearch";

    public string SinkKind => KindKey;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);
        return new ElasticsearchLogSink(definition.Uri, levelRegistry);
    }
}
