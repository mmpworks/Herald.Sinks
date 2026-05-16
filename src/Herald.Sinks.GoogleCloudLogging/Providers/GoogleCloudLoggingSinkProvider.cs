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

namespace Herald.Sinks.GoogleCloudLogging.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="GoogleCloudLoggingSink"/>
/// from a <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// Wire-up:
/// <list type="bullet">
///   <item><c>Uri</c> → GCP project id (required).</item>
///   <item><c>Host</c> → log id, default <c>herald</c>.</item>
/// </list>
/// MonitoredResource defaults to <c>global</c> — override via the
/// code-first ctor for production workloads on GCE / GKE / Cloud Run.
/// </remarks>
public sealed class GoogleCloudLoggingSinkProvider : ILogSinkProvider
{
    public const string KindKey = "gcp_logging";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);

        var logId = string.IsNullOrWhiteSpace(definition.Host) ? "herald" : definition.Host;

        return new GoogleCloudLoggingSink(
            projectId: definition.Uri,
            logId: logId);
    }
}
