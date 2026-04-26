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

namespace Herald.Sinks.Axiom.Providers;

public sealed class AxiomLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "axiom";
    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Pro;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Host);
        return new AxiomLogSink(definition.Uri, definition.Host);
    }
}
