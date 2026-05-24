// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using MMP.Herald.Configuration.Runtime;

namespace Herald.Sinks.GenericWebhook.Providers;

/// <summary>
/// Maps a <see cref="LoggingRuntimeSinkDefinition"/> into the values
/// <see cref="GenericWebhookLogSink"/>'s constructor needs.
///
/// <para>
/// The mmpform exposes three fields:
/// <list type="bullet">
///   <item><c>url</c> — webhook URL (required).</item>
///   <item><c>headers</c> — comma-separated <c>Name=Value</c> pairs
///         that ride on every outgoing request. The key path for
///         Bearer tokens, API keys, tenant tags, and signed-request
///         schemes.</item>
///   <item><c>content_type</c> — Content-Type header for the POST
///         body (default <c>application/json</c>).</item>
/// </list>
/// Per Richard's audit (BLOCKER for GenericWebhook): the prior
/// provider passed only <c>Uri</c> and dropped headers on the floor,
/// which made the sink useless against any endpoint that required
/// auth. Surfacing headers through the bag closes the BLOCKER.
/// </para>
///
/// <para>
/// Rules engine config (the <c>rules</c> ctor arg) stays code-first
/// via <see cref="GenericWebhookSinkProvider"/>'s ctor — operators
/// who need rule-driven dispatch register the provider with a
/// rules list at composition time. JSON-config serialisation of
/// rules is a separate piece of work.
/// </para>
/// </summary>
internal static class GenericWebhookSinkRuntimeConfig
{
    private const string KeyUrl         = "url";
    private const string KeyHeaders     = "headers";
    private const string KeyContentType = "content_type";

    /// <summary>Default Content-Type — matches the sink ctor default.</summary>
    public const string DefaultContentType = "application/json";

    /// <summary>
    /// Resolved GenericWebhook sink config. <see cref="Url"/> stays
    /// nullable so the provider can fail with a named
    /// ArgumentException. <see cref="ContentType"/> always carries
    /// a value (default <c>application/json</c>).
    /// </summary>
    public readonly record struct Resolved(
        string? Url,
        IReadOnlyDictionary<string, string>? Headers,
        string ContentType);

    public static Resolved From(LoggingRuntimeSinkDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var bag = definition.Properties;
        return new Resolved(
            Url:         ReadString(bag, KeyUrl)         ?? Nullify(definition.Uri),
            Headers:     ParseHeaderPairs(ReadString(bag, KeyHeaders)),
            ContentType: ReadString(bag, KeyContentType) ?? DefaultContentType);
    }

    // Headers arrive as "Name=Value, Name=Value". Same parser shape as
    // PagerDuty.custom_details_template and GoogleCloudLogging.resource_labels —
    // split on commas, split on the first '='. Bad pairs drop silently.
    private static IReadOnlyDictionary<string, string>? ParseHeaderPairs(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var pairs = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (pairs.Length == 0) return null;

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in pairs)
        {
            var eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            var key = pair[..eq].Trim();
            var value = pair[(eq + 1)..].Trim();
            if (string.IsNullOrEmpty(key)) continue;
            result[key] = value;
        }
        return result.Count == 0 ? null : result;
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
