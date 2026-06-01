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

namespace Herald.Sinks.Graylog.Providers;

public sealed class GraylogLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "graylog";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);

        var transport = string.Equals(definition.Alias, "tcp", StringComparison.OrdinalIgnoreCase)
            ? GraylogTransport.Tcp
            : GraylogTransport.Http;

        var sink = new GraylogLogSink(
            endpoint: definition.Uri,
            transport: transport,
            sourceHost: definition.Host);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }
}
