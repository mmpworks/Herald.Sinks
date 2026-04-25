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

namespace Herald.Sinks.AzureTableStorage.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="AzureTableStorageLogSink"/>
/// from a <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><c>Uri</c> → connection string (required).</item>
///   <item><c>Host</c> → table name (default <c>HeraldLogs</c>).</item>
/// </list>
/// </remarks>
public sealed class AzureTableStorageLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "azure_table_storage";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);

        var tableName = string.IsNullOrWhiteSpace(definition.Host) ? "HeraldLogs" : definition.Host;
        return new AzureTableStorageLogSink(definition.Uri, tableName);
    }
}
