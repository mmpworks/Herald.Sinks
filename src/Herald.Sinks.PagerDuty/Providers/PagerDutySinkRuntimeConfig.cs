// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using MMP.Herald.Configuration.Runtime;

namespace Herald.Sinks.PagerDuty.Providers;

/// <summary>
/// Maps a <see cref="LoggingRuntimeSinkDefinition"/> into the values
/// <see cref="PagerDutyLogSink"/>'s constructor needs.
///
/// <para>
/// The mmpform exposes seven operator-facing fields:
/// <list type="bullet">
///   <item><c>routing_key</c> — PagerDuty integration key (required).</item>
///   <item><c>source</c> — Events API source field
///         (default: machine name).</item>
///   <item><c>component</c> / <c>group</c> — optional categorisation
///         tags surfaced in the PagerDuty incident view.</item>
///   <item><c>endpoint</c> — enqueue URL (default
///         <c>https://events.pagerduty.com/v2/enqueue</c>).</item>
///   <item><c>dedup_strategy</c> — picks how the sink derives
///         <c>dedup_key</c>: <c>auto</c>, <c>event_id</c>,
///         <c>template</c>, <c>category</c>, or <c>message</c>.</item>
///   <item><c>custom_details_template</c> — static fields merged into
///         <c>payload.custom_details</c> as comma-separated
///         <c>key=value</c> pairs.</item>
/// </list>
/// Per Richard's audit (BLOCKER for PagerDuty): dedup_key and
/// custom_details are the two configuration points operators rely on
/// most for PagerDuty hygiene; surfacing them through the form closes
/// the BLOCKER.
/// </para>
/// </summary>
internal static class PagerDutySinkRuntimeConfig
{
    private const string KeyRoutingKey           = "routing_key";
    private const string KeySource               = "source";
    private const string KeyComponent            = "component";
    private const string KeyGroup                = "group";
    private const string KeyEndpoint             = "endpoint";
    private const string KeyDedupStrategy        = "dedup_strategy";
    private const string KeyCustomDetailsTpl     = "custom_details_template";

    /// <summary>
    /// Resolved PagerDuty sink config. <see cref="RoutingKey"/> stays
    /// nullable so the provider can fail with a named
    /// ArgumentException. <see cref="DedupStrategy"/> always carries
    /// a value — <see cref="PagerDutyDedupStrategy.Auto"/> when the
    /// bag is silent or carries an unrecognised string.
    /// </summary>
    public readonly record struct Resolved(
        string? RoutingKey,
        string? Source,
        string? Component,
        string? Group,
        string? Endpoint,
        PagerDutyDedupStrategy DedupStrategy,
        IReadOnlyDictionary<string, string>? CustomDetailsTemplate);

    public static Resolved From(LoggingRuntimeSinkDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var bag = definition.Properties;
        return new Resolved(
            RoutingKey:           ReadString(bag, KeyRoutingKey) ?? Nullify(definition.Alias),
            Source:               ReadString(bag, KeySource)     ?? Nullify(definition.Host),
            Component:            ReadString(bag, KeyComponent),
            Group:                ReadString(bag, KeyGroup),
            Endpoint:             ReadString(bag, KeyEndpoint)   ?? Nullify(definition.Uri),
            DedupStrategy:        ParseDedupStrategy(ReadString(bag, KeyDedupStrategy)),
            CustomDetailsTemplate: ParseTemplatePairs(ReadString(bag, KeyCustomDetailsTpl)));
    }

    // Strategy vocabulary mirrors the mmpform tooltip text. Unknown
    // strings fall back to Auto — the safe choice that preserves the
    // pre-strategy behaviour. Operators making a typo in the form get
    // the existing fallback chain rather than a hard failure.
    private static PagerDutyDedupStrategy ParseDedupStrategy(string? raw) =>
        (raw ?? "").Trim().ToLowerInvariant() switch
        {
            "event_id" => PagerDutyDedupStrategy.EventId,
            "template" => PagerDutyDedupStrategy.Template,
            "category" => PagerDutyDedupStrategy.Category,
            "message"  => PagerDutyDedupStrategy.Message,
            _          => PagerDutyDedupStrategy.Auto,
        };

    // custom_details_template arrives as "k1=v1, k2=v2". Same shape as
    // the GoogleCloudLogging resource_labels parser — comma-split then
    // split-on-first-'='. Bad pairs drop silently.
    private static IReadOnlyDictionary<string, string>? ParseTemplatePairs(string? raw)
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
