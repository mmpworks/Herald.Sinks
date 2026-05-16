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

namespace Herald.Sinks.Exceptionless.Providers;

public sealed class ExceptionlessLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "exceptionless";
    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Alias);
        return new ExceptionlessLogSink(
            apiKey: definition.Alias,
            serverUrl: string.IsNullOrWhiteSpace(definition.Uri) ? "https://collector.exceptionless.io" : definition.Uri);
    }
}
