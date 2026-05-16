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

namespace Herald.Sinks.UnityConsole.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="UnityConsoleLogSink"/>
/// from a <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// <para>
/// JSON-config wiring is not the primary path for this sink — the
/// consumer typically constructs the sink in code and registers it
/// with their Unity-aware bootstrap so they can pass real
/// <c>UnityEngine.Debug.Log</c> delegates. This provider exists so
/// the JSON-config path doesn't surface an unsupported-sink error;
/// when invoked, it constructs the sink with no-op delegates and
/// surfaces a diagnostic via the formatted line itself, prompting
/// the consumer to switch to code-first wiring.
/// </para>
/// </remarks>
public sealed class UnityConsoleLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "unity_console";

    public string SinkKind => KindKey;
    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);

        // No-op delegates — the JSON-config path can't wire real
        // UnityEngine.Debug methods because the package doesn't
        // reference UnityEngine. Consumers who want the JSON path
        // should subclass this provider and override CreateSink to
        // pass their own Debug.* delegates.
        return new UnityConsoleLogSink(
            log:      _ => { },
            warn:     _ => { },
            error:    _ => { },
            category: definition.Alias);
    }
}
