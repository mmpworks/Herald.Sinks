// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using Azure.Data.Tables;
using Azure.Identity;
using MMP.Herald;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Levels;
using MMP.Herald.Output.Rendering;
using MMP.Herald.Pipeline;
using MMP.Herald.Routing;
using MMP.Herald.Sinks.Batching;

namespace Herald.Sinks.AzureTableStorage.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="AzureTableStorageLogSink"/>
/// from a <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// <para>
/// Wire-up conventions:
/// </para>
/// <list type="bullet">
///   <item><c>Uri</c> holds either a full Azure Storage connection
///     string (starts with <c>DefaultEndpointsProtocol=</c> or
///     otherwise contains <c>AccountKey</c>) OR the table-service
///     endpoint URL (<c>https://{account}.table.core.windows.net</c>).
///     The endpoint-URL form authenticates through
///     <see cref="DefaultAzureCredential"/> — managed identity in
///     production, local dev credentials on a workstation — so no
///     secret needs to live in config when the host has an assigned
///     identity.</item>
///   <item><c>Host</c> holds the table name (default <c>HeraldLogs</c>).</item>
///   <item><c>Alias</c> optionally selects a partition strategy:
///     <c>"day"</c> (default), <c>"hour"</c>, <c>"minute"</c>, or
///     <c>"fixed"</c>.</item>
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
        var strategy = ParseStrategy(definition.Alias);
        var tableClient = BuildTableClient(definition.Uri!, tableName);

        var sink = new AzureTableStorageLogSink(
            tableClient: tableClient,
            partitionStrategy: strategy);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }

    // Pick the auth path from the shape of the Uri field. A URL
    // (http/https) is treated as the service endpoint and
    // authenticated through DefaultAzureCredential; anything else is
    // treated as a connection string. Mirrors AzureBlobArchiveProvider.
    private static TableClient BuildTableClient(string uri, string tableName)
    {
        if (LooksLikeEndpointUrl(uri))
        {
            var endpoint = new Uri(uri);
            var client = new TableClient(endpoint, tableName, new DefaultAzureCredential());
            client.CreateIfNotExists();
            return client;
        }
        var sharedKeyClient = new TableClient(uri, tableName);
        sharedKeyClient.CreateIfNotExists();
        return sharedKeyClient;
    }

    private static bool LooksLikeEndpointUrl(string value) =>
        value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

    private static AzureTablePartitionKeyStrategy ParseStrategy(string? alias)
    {
        return (alias ?? "day").ToLowerInvariant() switch
        {
            "day" => AzureTablePartitionKeyStrategy.UtcDay,
            "hour" => AzureTablePartitionKeyStrategy.UtcHour,
            "minute" => AzureTablePartitionKeyStrategy.UtcMinute,
            "fixed" => AzureTablePartitionKeyStrategy.Fixed,
            _ => AzureTablePartitionKeyStrategy.UtcDay
        };
    }
}
