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

namespace Herald.Sinks.OpenSearch.Providers;

public sealed class OpenSearchLogSinkProvider : BatchingSinkProviderBase
{
    public const string KindKey = "opensearch";

    public override string SinkKind => KindKey;
    public override HeraldEdition MinimumEdition => HeraldEdition.Community;

    public override ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);

        var sink = new OpenSearchLogSink(
            endpoint: definition.Uri,
            indexNameTemplate: string.IsNullOrWhiteSpace(definition.Host)
                ? "herald-logs-{0:yyyy-MM-dd}"
                : definition.Host,
            username: definition.Alias,
            password: null);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }
}
