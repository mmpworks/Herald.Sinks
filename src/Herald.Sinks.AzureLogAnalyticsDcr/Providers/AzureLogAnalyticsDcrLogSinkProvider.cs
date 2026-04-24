// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using MMP.Herald;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Levels;
using MMP.Herald.Output.Rendering;
using MMP.Herald.Pipeline;
using MMP.Herald.Routing;

namespace Herald.Sinks.AzureLogAnalyticsDcr.Providers;

/// <summary>
/// Sink provider for the DCR-based Azure Monitor ingest sink.
/// </summary>
/// <remarks>
/// Wire-up:
/// <list type="bullet">
///   <item><c>Uri</c> → DCE logs-ingestion endpoint (required).</item>
///   <item><c>Alias</c> → DCR immutable ID (required). Starts with <c>dcr-</c>.</item>
///   <item><c>Host</c> → stream name (required). Starts with <c>Custom-</c>.</item>
/// </list>
/// Credentials resolve through DefaultAzureCredential. Callers that
/// need explicit credentials construct the sink via the code-first
/// ctor.
/// </remarks>
public sealed class AzureLogAnalyticsDcrLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "azure_log_analytics_dcr";

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
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Host);

        return new AzureLogAnalyticsDcrLogSink(
            endpoint: definition.Uri,
            ruleId: definition.Alias,
            streamName: definition.Host);
    }
}
