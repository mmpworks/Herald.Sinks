// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Azure.Monitor.Ingestion;
using MMP.Herald;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;
// Azure ships its own LogEvent-adjacent types; alias for clarity.
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.AzureLogAnalyticsDcr;

/// <summary>
/// Sink that writes Herald log events to Azure Monitor via the modern
/// <b>Data Collection Rule (DCR)</b>-based Log Ingestion API —
/// Microsoft's supported successor to the legacy HTTP Data Collector
/// API. Use this instead of <c>Herald.Sinks.AzureAnalytics</c> for any
/// new workspace; Data Collector is in end-of-life notice.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three IDs to gather.</b> The DCR path needs three identifiers
/// from the Azure portal:
/// <list type="bullet">
///   <item>The DCR's <b>Logs Ingestion endpoint</b> (e.g.
///   <c>https://dce-XXXX.eastus-1.ingest.monitor.azure.com</c>).</item>
///   <item>The DCR's <b>Immutable ID</b> (starts with <c>dcr-</c>).</item>
///   <item>The target <b>stream name</b> declared in the DCR
///   (starts with <c>Custom-</c>).</item>
/// </list>
/// </para>
/// <para>
/// <b>Auth.</b> Uses <see cref="DefaultAzureCredential"/> unless a
/// <see cref="TokenCredential"/> is supplied. Grant the principal the
/// <c>Monitoring Metrics Publisher</c> role on the DCR.
/// </para>
/// </remarks>
public sealed class AzureLogAnalyticsDcrLogSink : ILogger, IBatchedLogSink
{
    private readonly LogsIngestionClient _client;
    private readonly string _ruleId;
    private readonly string _streamName;

    public AzureLogAnalyticsDcrLogSink(
        string endpoint,
        string ruleId,
        string streamName,
        TokenCredential? credential = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);

        _ruleId = ruleId;
        _streamName = streamName;
        _client = new LogsIngestionClient(new Uri(endpoint), credential ?? new DefaultAzureCredential());
    }

    /// <summary>
    /// Code-first overload for callers that already own a
    /// <see cref="LogsIngestionClient"/>.
    /// </summary>
    public AzureLogAnalyticsDcrLogSink(
        LogsIngestionClient client,
        string ruleId,
        string streamName)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);

        _client = client;
        _ruleId = ruleId;
        _streamName = streamName;
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

        var records = new List<BinaryData>(events.Count);
        foreach (var evt in events)
        {
            records.Add(BinaryData.FromString(BuildRecord(evt)));
        }

        _client.Upload(_ruleId, _streamName, records);
    }

    private static string BuildRecord(LogEvent evt)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            // TimeGenerated is the canonical column name Azure Monitor
            // uses for event timestamps on DCR-declared custom tables.
            writer.WriteString("TimeGenerated", evt.TimeUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("Level", evt.Level.Key);
            writer.WriteString("Category", evt.Category.Value);
            writer.WriteString("Message", evt.Message ?? string.Empty);
            writer.WriteString("Template", evt.MessageTemplate ?? string.Empty);

            if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value) && value is Exception ex)
            {
                writer.WriteString("Exception", ex.ToString());
                writer.WriteString("ExceptionType", ex.GetType().FullName ?? ex.GetType().Name);
            }

            if (evt.Properties is not null && evt.Properties.Count > 0)
            {
                foreach (var prop in evt.Properties)
                {
                    writer.WriteString("Prop_" + prop.Name, prop.ResolvedValue?.ToString());
                }
            }

            writer.WriteEndObject();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
