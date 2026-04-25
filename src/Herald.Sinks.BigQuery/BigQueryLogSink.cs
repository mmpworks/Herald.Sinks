// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using Google.Cloud.BigQuery.V2;
using MMP.Herald;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.BigQuery;

/// <summary>
/// Sink that streaming-inserts log events into a BigQuery table via
/// Google.Cloud.BigQuery.V2.
/// </summary>
/// <remarks>
/// Expected schema:
/// <list type="bullet">
///   <item>time_utc TIMESTAMP REQUIRED</item>
///   <item>level STRING REQUIRED</item>
///   <item>category STRING NULLABLE</item>
///   <item>message STRING NULLABLE</item>
///   <item>template STRING NULLABLE</item>
/// </list>
/// </remarks>
public sealed class BigQueryLogSink : ILogger, IBatchedLogSink
{
    private readonly BigQueryClient _client;
    private readonly string _datasetId;
    private readonly string _tableId;

    public BigQueryLogSink(string projectId, string datasetId, string tableId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableId);

        _client = BigQueryClient.Create(projectId);
        _datasetId = datasetId;
        _tableId = tableId;
    }

    public BigQueryLogSink(BigQueryClient client, string datasetId, string tableId)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableId);

        _client = client;
        _datasetId = datasetId;
        _tableId = tableId;
    }

    public void Log(LogEvent logEvent) => LogBatch(new[] { logEvent });

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        var rows = new List<BigQueryInsertRow>(events.Count);
        foreach (var evt in events)
        {
            rows.Add(new BigQueryInsertRow
            {
                ["time_utc"] = evt.TimeUtc.UtcDateTime,
                ["level"] = evt.Level.Key,
                ["category"] = evt.Category.Value,
                ["message"] = evt.Message ?? string.Empty,
                ["template"] = evt.MessageTemplate ?? string.Empty,
            });
        }

        _client.InsertRows(_datasetId, _tableId, rows);
    }
}
