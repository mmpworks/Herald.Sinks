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

namespace Herald.Sinks.ZeroMQ.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="ZeroMqLogSink"/> from a
/// <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><c>Uri</c> → ZeroMQ endpoint (required, e.g. <c>tcp://*:5555</c>).</item>
///   <item><c>Host</c> → topic prefix (default <c>herald-logs</c>).</item>
/// </list>
/// <para>
/// The provider always wires PubSub. For PushPull or custom bind/connect
/// behaviour, construct <see cref="ZeroMqLogSink"/> directly via the
/// code-first ctor.
/// </para>
/// </remarks>
public sealed class ZeroMqLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "zeromq";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);

        var topic = string.IsNullOrWhiteSpace(definition.Host) ? "herald-logs" : definition.Host;
        return new ZeroMqLogSink(definition.Uri, topic);
    }
}
