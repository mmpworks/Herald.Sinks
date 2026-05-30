// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Configuration.Sinks;

namespace Herald.Sinks.Trace.Providers;

/// <summary>
/// Maps a <see cref="LoggingRuntimeSinkDefinition"/> into the values
/// <see cref="TraceLogSink"/>'s constructor needs.
///
/// <para>
/// The Trace sink has one operator-facing knob — the trace category
/// prefix. <c>configuration-trace.mmpform</c> exposes it as the
/// <c>category</c> property. Older deployments authored before the
/// v2 sink-property bag existed populated the same value through the
/// definition's <c>Alias</c> slot; this mapper accepts either.
/// </para>
///
/// <para>
/// Reading order is bag-first: any populated <see cref="LoggingRuntimeSinkDefinition.Properties"/>
/// wins over the legacy slot. That lets a freshly-written mmpform
/// override an older dashboard JSON that still carries the value in
/// <c>Alias</c>, without breaking deployments that have not migrated.
/// </para>
///
/// <para>
/// Trace is the single-field-mapper reference shape: returns
/// <see cref="string"/>? directly rather than wrapping in a
/// <c>Resolved</c> record because there is only one knob to carry.
/// Multi-field sinks follow the per-sink <c>Resolved</c> shape; this
/// one stays scalar.
/// </para>
/// </summary>
internal static class TraceSinkRuntimeConfig
{
    private const string KeyCategory = "category";

    /// <summary>
    /// Resolve the category prefix from <paramref name="definition"/>.
    /// Returns <c>null</c> when neither the bag nor the legacy slot
    /// supplies a value — the sink treats that as "no category".
    /// </summary>
    public static string? ResolveCategory(LoggingRuntimeSinkDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        // Bag wins when present. SinkPropertyBag.ReadString returns
        // null for missing keys and for empty strings, so an
        // empty-string bag entry falls through to the legacy slot
        // — that matches operator intent ("blank in the form =
        // leave it alone").
        return SinkPropertyBag.ReadString(definition.Properties, KeyCategory)
               ?? SinkPropertyBag.Nullify(definition.Alias);
    }
}
