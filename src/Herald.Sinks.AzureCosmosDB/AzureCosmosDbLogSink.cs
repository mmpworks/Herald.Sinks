// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using MMP.Herald;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.AzureCosmosDB;

/// <summary>
/// Sink that writes log events as documents in an Azure Cosmos DB
/// container via the modern v3 SDK. Drop-in for
/// Serilog.Sinks.AzureCosmosDB (the maintained one — the legacy
/// <c>AzureDocumentDb</c> package uses the deprecated SDK).
/// </summary>
/// <remarks>
/// <para>
/// <b>Document shape.</b> Each event becomes a JSON document with
/// <c>id</c> (GUID), <c>timeUtc</c>, <c>level</c>, <c>category</c>,
/// <c>message</c>, <c>template</c>, optional <c>exception</c>, and
/// optional <c>properties</c> object. Partition key defaults to the
/// category value — override by pre-setting the container's partition
/// key path and supplying a matching property through context.
/// </para>
/// <para>
/// <b>TTL.</b> Enable the container's time-to-live at deploy time for
/// log retention. The sink does not set a per-item <c>ttl</c> field in
/// 1.0 — follow-up feature.
/// </para>
/// </remarks>
public sealed class AzureCosmosDbLogSink : ILogger, IBatchedLogSink, IDisposable
{
    private readonly CosmosClient _client;
    private readonly bool _ownsClient;
    private readonly Container _container;
    private readonly string _partitionKeyProperty;

    public AzureCosmosDbLogSink(
        string endpoint,
        string authKey,
        string databaseName,
        string containerName,
        string partitionKeyProperty = "category")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(authKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKeyProperty);

        _client = new CosmosClient(endpoint, authKey);
        _ownsClient = true;
        _container = _client.GetContainer(databaseName, containerName);
        _partitionKeyProperty = partitionKeyProperty;
    }

    /// <summary>
    /// Code-first overload for callers that already own a CosmosClient.
    /// </summary>
    public AzureCosmosDbLogSink(
        CosmosClient client,
        string databaseName,
        string containerName,
        string partitionKeyProperty = "category")
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKeyProperty);

        _client = client;
        _ownsClient = false;
        _container = _client.GetContainer(databaseName, containerName);
        _partitionKeyProperty = partitionKeyProperty;
    }

    public void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        LogBatch(new[] { logEvent });
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        // Cosmos v3 has no native bulk-create; we fire CreateItemAsync
        // per event. Cosmos SDK's bulk-execution mode (via
        // CosmosClientOptions.AllowBulkExecution = true on the client
        // factory) batches these internally when the client opts in —
        // we keep the per-call path so the sink works with default
        // client settings.
        foreach (var evt in events)
        {
            var (doc, partitionKey) = BuildDocument(evt);
            _container.CreateItemAsync(doc, partitionKey).GetAwaiter().GetResult();
        }
    }

    public void Dispose()
    {
        if (_ownsClient) _client.Dispose();
    }

    private (string Document, PartitionKey PartitionKey) BuildDocument(LogEvent evt)
    {
        var partitionValue = evt.Category.Value;

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("id", Guid.NewGuid().ToString("N"));
            writer.WriteString("timeUtc", evt.TimeUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("level", evt.Level.Key);
            writer.WriteString("category", evt.Category.Value);
            writer.WriteString("message", evt.Message ?? string.Empty);
            writer.WriteString("template", evt.MessageTemplate ?? string.Empty);

            if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value) && value is Exception ex)
            {
                writer.WriteString("exception", ex.ToString());
                writer.WriteString("exceptionType", ex.GetType().FullName ?? ex.GetType().Name);
            }

            if (evt.Properties is not null && evt.Properties.Count > 0)
            {
                writer.WriteStartObject("properties");
                foreach (var prop in evt.Properties)
                {
                    writer.WriteString(prop.Name, prop.ResolvedValue?.ToString());
                }
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.Flush();
        }

        var docJson = Encoding.UTF8.GetString(stream.ToArray());
        return (docJson, new PartitionKey(partitionValue));
    }
}
