// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using Google.Api;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Configuration.Sinks;

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
        var projectId    = SinkPropertyBag.ReadString(bag, KeyProjectId)    ?? SinkPropertyBag.Nullify(definition.Uri);
        var logId        = SinkPropertyBag.ReadString(bag, KeyLogId)        ?? SinkPropertyBag.Nullify(definition.Host) ?? DefaultLogId;
        var resourceType = SinkPropertyBag.ReadString(bag, KeyResourceType) ?? DefaultResourceType;

        var resource = new MonitoredResource { Type = resourceType };
        var labels = SinkPropertyBag.ReadKeyValuePairs(bag, KeyResourceLabels);
        if (labels is not null)
        {
            foreach (var pair in labels)
            {
                // MonitoredResource.Labels is a protobuf map; the
                // parser already dropped blank keys and pairs without
                // '=', so the loop just forwards what landed.
                resource.Labels[pair.Key] = pair.Value;
            }
        }

        return new Resolved(projectId, logId, resource);
    }
}
