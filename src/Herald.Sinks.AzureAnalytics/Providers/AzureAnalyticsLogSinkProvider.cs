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

namespace Herald.Sinks.AzureAnalytics.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="AzureAnalyticsLogSink"/>
/// from a <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// Wire-up:
/// <list type="bullet">
///   <item><c>Uri</c> → workspace id (required).</item>
///   <item><c>Alias</c> → workspace key / shared key (required).</item>
///   <item><c>Host</c> → custom Log-Type name, default <c>HeraldLog</c>.</item>
/// </list>
/// </remarks>
public sealed class AzureAnalyticsLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "azure_analytics";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Alias);

        var logType = string.IsNullOrWhiteSpace(definition.Host) ? "HeraldLog" : definition.Host;

        return new AzureAnalyticsLogSink(
            workspaceId: definition.Uri,
            workspaceKey: definition.Alias,
            logType: logType);
    }
}
