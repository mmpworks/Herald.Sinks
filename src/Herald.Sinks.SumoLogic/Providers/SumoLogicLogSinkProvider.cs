// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using MMP.Herald;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Levels;
using MMP.Herald.Output.Rendering;
using MMP.Herald.Pipeline;
using MMP.Herald.Routing;

namespace Herald.Sinks.SumoLogic.Providers;

public sealed class SumoLogicLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "sumologic";
    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Pro;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);
        return new SumoLogicLogSink(
            sourceUrl: definition.Uri,
            sourceCategory: definition.Alias,
            sourceHost: definition.Host);
    }
}
