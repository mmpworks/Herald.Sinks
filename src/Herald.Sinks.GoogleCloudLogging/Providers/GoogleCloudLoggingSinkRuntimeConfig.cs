// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using Google.Api;
using MMP.Herald.Configuration.Runtime;

namespace Herald.Sinks.GoogleCloudLogging.Providers;

/// <summary>
/// Maps a <see cref="LoggingRuntimeSinkDefinition"/> into the values
/// <see cref="GoogleCloudLoggingSink"/>'s constructor needs.
///
/// <para>
/// The mmpform exposes four fields:
/// <list type="bullet">
///   <item><c>project_id</c> — GCP project id (required).</item>
///   <item><c>log_id</c> — log id within the project (default <c>herald</c>).</item>
///   <item><c>resource_type</c> — monitored-resource type (default <c>global</c>).</item>
///   <item><c>resource_labels</c> — comma-separated <c>key=value</c>
///         pairs that populate the monitored resource's labels map.</item>
/// </list>
/// Per Richard's audit (BLOCKER for GoogleCloudLogging): GCP routes
/// log entries to dashboards by resource labels. Without them, every
/// entry lands under <c>global</c> with no labels — invisible to the
/// per-service / per-pod views that operators actually use.
/// </para>
/// </summary>
internal static class GoogleCloudLoggingSinkRuntimeConfig
{
    private const string KeyProjectId      = "project_id";
    private const string KeyLogId          = "log_id";
    private const string KeyResourceType   = "resource_type";
    private const string KeyResourceLabels = "resource_labels";

    /// <summary>Default log id — matches the sink ctor default.</summary>
    public const string DefaultLogId = "herald";

    /// <summary>Default monitored-resource type for on-prem / local dev.</summary>
    public const string DefaultResourceType = "global";

    /// <summary>
    /// Resolved Google Cloud Logging sink config. <see cref="ProjectId"/>
    /// stays nullable so the provider can fail with a named
    /// ArgumentException. <see cref="Resource"/> always carries a
    /// MonitoredResource — at minimum the default <c>global</c> with
    /// no labels.
    /// </summary>
    public readonly record struct Resolved(
        string? ProjectId,
        string LogId,
        MonitoredResource Resource);

    public static Resolved From(LoggingRuntimeSinkDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var bag = definition.Properties;
        var projectId    = ReadString(bag, KeyProjectId)    ?? Nullify(definition.Uri);
        var logId        = ReadString(bag, KeyLogId)        ?? Nullify(definition.Host) ?? DefaultLogId;
        var resourceType = ReadString(bag, KeyResourceType) ?? DefaultResourceType;
        var labelsRaw    = ReadString(bag, KeyResourceLabels);

        var resource = new MonitoredResource { Type = resourceType };
        foreach (var (key, value) in ParseLabelPairs(labelsRaw))
        {
            // MonitoredResource.Labels is a protobuf map; duplicate
            // keys overwrite (last-wins). The parser drops empty keys,
            // so a stray comma in the operator's input doesn't crash.
            resource.Labels[key] = value;
        }

        return new Resolved(projectId, logId, resource);
    }

    // resource_labels arrives as "k1=v1, k2=v2, k3=v3". Splitting on
    // commas first, then on the first '=' in each pair, keeps the
    // mapper's contract local. Pairs that are blank or missing '='
    // silently drop — better than throwing on operator typos that
    // would have been caught by the form validator.
    private static IEnumerable<KeyValuePair<string, string>> ParseLabelPairs(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) yield break;

        var pairs = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var pair in pairs)
        {
            var eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            var key = pair[..eq].Trim();
            var value = pair[(eq + 1)..].Trim();
            if (string.IsNullOrEmpty(key)) continue;
            yield return new KeyValuePair<string, string>(key, value);
        }
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
