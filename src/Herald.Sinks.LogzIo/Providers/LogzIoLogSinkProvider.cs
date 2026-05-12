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

namespace Herald.Sinks.LogzIo.Providers;

public sealed class LogzIoLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "logzio";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Alias);

        return new LogzIoLogSink(
            accountToken: definition.Alias,
            type: string.IsNullOrWhiteSpace(definition.Host) ? "herald" : definition.Host,
            listenerUrl: string.IsNullOrWhiteSpace(definition.Uri) ? "https://listener.logz.io:8071/" : definition.Uri);
    }
}
