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

namespace Herald.Sinks.Trace.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="TraceLogSink"/> from a
/// <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// <para>
/// Configuration sources, in priority order:
/// <list type="bullet">
///   <item><b>v2 property bag</b> (<c>configuration-trace.mmpform</c> → <see cref="LoggingRuntimeSinkDefinition.Properties"/>):
///     <list type="bullet">
///       <item><c>category</c> — trace category prefix forwarded to
///             <see cref="System.Diagnostics.Trace"/> listeners.</item>
///     </list>
///   </item>
///   <item><b>Legacy slot</b> (pre-v2 dashboard JSON): <see cref="LoggingRuntimeSinkDefinition.Alias"/>
///         carried the same category prefix. Read only when the bag
///         is absent or empty so existing deployments keep working
///         without form re-entry.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class TraceLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "trace";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);

        // Bag-first, legacy-fallback resolution kept inside the mapper
        // so the provider stays a one-line dispatch. Mirrors the
        // FileSinkRuntimeConfig pattern.
        var category = TraceSinkRuntimeConfig.ResolveCategory(definition);
        return new TraceLogSink(category: category);
    }
}
