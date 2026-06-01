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

namespace Herald.Sinks.LogzIo.Providers;

public sealed class LogzIoLogSinkProvider : BatchingSinkProviderBase
{
    public const string KindKey = "logzio";

    public override string SinkKind => KindKey;
    public override HeraldEdition MinimumEdition => HeraldEdition.Community;

    public override ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Alias);

        var sink = new LogzIoLogSink(
            accountToken: definition.Alias,
            type: string.IsNullOrWhiteSpace(definition.Host) ? "herald" : definition.Host,
            listenerUrl: string.IsNullOrWhiteSpace(definition.Uri) ? "https://listener.logz.io:8071/" : definition.Uri);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }
}
