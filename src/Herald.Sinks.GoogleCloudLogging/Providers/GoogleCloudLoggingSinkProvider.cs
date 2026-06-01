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
using MMP.Herald.Sinks.Batching;

namespace Herald.Sinks.GoogleCloudLogging.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="GoogleCloudLoggingSink"/>
/// from a <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// <para>
/// Configuration sources, in priority order:
/// <list type="bullet">
///   <item><b>v2 property bag</b> (<c>configuration-gcp_logging.mmpform</c> → <see cref="LoggingRuntimeSinkDefinition.Properties"/>):
///     <list type="bullet">
///       <item><c>project_id</c> — GCP project id (required).</item>
///       <item><c>log_id</c> — log id within the project (default <c>herald</c>).</item>
///       <item><c>resource_type</c> — monitored-resource type (default <c>global</c>).</item>
///       <item><c>resource_labels</c> — comma-separated <c>key=value</c>
///             pairs that populate the monitored resource's labels map.</item>
///     </list>
///   </item>
///   <item><b>Legacy slots</b>:
///     <list type="bullet">
///       <item><see cref="LoggingRuntimeSinkDefinition.Uri"/> ↔ project_id.</item>
///       <item><see cref="LoggingRuntimeSinkDefinition.Host"/> ↔ log_id.</item>
///     </list>
///     Resource type and labels had no legacy slot — older deployments
///     had no way to set them and landed under <c>global</c> with no
///     labels. That's the GCP routing bug Richard's audit calls out as
///     a BLOCKER.
///   </item>
/// </list>
/// Auth still uses Application Default Credentials (ADC); the sink
/// builds its own <see cref="Google.Cloud.Logging.V2.LoggingServiceV2Client"/>
/// at construction. Apps that already own a client use the code-first
/// overload on <see cref="GoogleCloudLoggingSink"/>.
/// </para>
/// </remarks>
public sealed class GoogleCloudLoggingSinkProvider : BatchingSinkProviderBase
{
    public const string KindKey = "gcp_logging";

    public override string SinkKind => KindKey;
    public override HeraldEdition MinimumEdition => HeraldEdition.Community;

    public override ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var resolved = GoogleCloudLoggingSinkRuntimeConfig.From(definition);

        if (string.IsNullOrWhiteSpace(resolved.ProjectId))
        {
            throw new ArgumentException(
                $"GoogleCloudLogging sink '{definition.Name}' is missing the required 'project_id' " +
                $"property (or legacy Uri). Set it in configuration-gcp_logging.mmpform.",
                nameof(definition));
        }

        var sink = new GoogleCloudLoggingSink(
            projectId: resolved.ProjectId!,
            logId: resolved.LogId,
            resource: resolved.Resource);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }
}
