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

namespace Herald.Sinks.GodotConsole.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="GodotConsoleLogSink"/>
/// from a <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// Wire-up: <c>Alias</c> is forwarded as the sink's category prefix.
/// Leaving it unset produces lines without a prefix.
/// </remarks>
public sealed class GodotConsoleLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "godot_console";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new GodotConsoleLogSink(category: definition.Alias);
    }
}
