// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Globalization;
using MMP.Herald;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Levels;
using MMP.Herald.Output.Rendering;
using MMP.Herald.Pipeline;
using MMP.Herald.Routing;

namespace Herald.Sinks.InMemory.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="InMemoryLogSink"/> from a
/// <see cref="LoggingRuntimeSinkDefinition"/>. Rare in practice — most
/// tests construct the sink directly and register it via
/// <c>WithCustomSinkProvider</c> so they hold a reference to the sink
/// and can read <c>Events</c> from it.
/// </summary>
/// <remarks>
/// <para>
/// Wire-up: <c>Alias</c>, when parseable as a positive integer, becomes
/// the retention capacity. Leaving it unset produces an unbounded sink.
/// </para>
/// </remarks>
public sealed class InMemoryLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "in_memory";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);

        int? capacity = null;
        if (!string.IsNullOrWhiteSpace(definition.Alias)
            && int.TryParse(definition.Alias, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0)
        {
            capacity = parsed;
        }

        return new InMemoryLogSink(capacity);
    }
}
