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

namespace Herald.Sinks.Debug.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="DebugLogSink"/> from a
/// <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// Wire-up: <c>Alias</c> is forwarded as the
/// <see cref="DebugLogSink"/>'s category prefix so Visual Studio's
/// Output window shows the source. Leaving it unset works fine and
/// produces un-categorized lines.
/// </remarks>
public sealed class DebugLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "debug";

    public string SinkKind => KindKey;

    // Community tier — the sink is a few dozen lines of BCL plumbing
    // with no external dependencies, edition-gating would just hide a
    // zero-cost diagnostic path.
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new DebugLogSink(category: definition.Alias);
    }
}
