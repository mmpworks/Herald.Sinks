// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using MMP.Herald.Configuration.Runtime;

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

        // Bag wins when present. ReadString returns null for missing
        // keys and for empty strings, so an empty-string bag entry
        // falls through to the legacy slot — that matches operator
        // intent ("blank in the form = leave it alone").
        var fromBag = ReadString(definition.Properties, KeyCategory);
        if (fromBag is not null) return fromBag;

        return Nullify(definition.Alias);
    }

    private static string? ReadString(
        IReadOnlyDictionary<string, object?>? bag, string key)
    {
        if (bag is null) return null;
        if (!bag.TryGetValue(key, out var raw) || raw is null) return null;
        var text = raw.ToString();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static string? Nullify(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
