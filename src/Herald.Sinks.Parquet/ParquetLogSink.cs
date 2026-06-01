// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using Herald.Sinks.Parquet.Iceberg;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;
using Parquet;
using Parquet.Data;
using Parquet.Schema;

namespace Herald.Sinks.Parquet;

/// <summary>
/// Sink that writes log events as Apache Parquet files. One Parquet file
/// per <see cref="LogBatch(IReadOnlyList{LogEvent})"/> call; the file
/// name is the configured prefix plus a UTC timestamp.
/// </summary>
/// <remarks>
/// <para>
/// <b>Schema (v1).</b> Seven columns: <c>time_utc</c> (TIMESTAMP_MICROS UTC),
/// <c>level</c> (UTF8), <c>category</c> (UTF8), <c>message</c> (UTF8),
/// <c>template</c> (UTF8), <c>exception</c> (UTF8 nullable),
/// <c>properties</c> (UTF8, JSON-encoded). The properties column is
/// stringified JSON for now — a future schema bump can move to
/// MAP&lt;STRING,STRING&gt; once the analytical consumers settle on a
/// shape.
/// </para>
/// <para>
/// <b>File-per-batch.</b> The simplest correct shape. Real analytical
/// workloads want larger row groups and fewer files; the natural
/// evolution is a rolling buffer (size or time threshold) similar to
/// the File sink. Tracked for v1.x.
/// </para>
/// <para>
/// <b>Iceberg.</b> The injected <see cref="IIcebergCatalogClient"/> is
/// notified after each file closes. The default
/// <see cref="NoOpIcebergCatalogClient"/> drops the notification — wire
/// a REST-catalog client when the catalog endpoint is available.
/// </para>
/// <para>
/// <b>Async/sync.</b> Parquet.Net is async-only. Both <see cref="ILogger.Log"/>
/// and <see cref="IBatchedLogSink.LogBatch"/> are sync, so the sink
/// blocks on the async writer via <c>GetAwaiter().GetResult()</c>. This
/// matches how other sinks in this repo bridge sync/async I/O.
/// </para>
/// </remarks>
public sealed class ParquetLogSink : HeraldSinkBase, IBatchedLogSink, INetworkSink
{
    private readonly string _outputDirectory;
    private readonly string _filePrefix;
    private readonly IIcebergCatalogClient _catalog;
    private readonly ParquetSchema _schema = BuildSchema();

    public ParquetLogSink(
        string outputDirectory,
        string filePrefix = "herald-logs",
        IIcebergCatalogClient? catalog = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePrefix);

        _outputDirectory = outputDirectory;
        _filePrefix = filePrefix;
        _catalog = catalog ?? NoOpIcebergCatalogClient.Instance;

        Directory.CreateDirectory(_outputDirectory);
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        LogBatch([logEvent]);
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        var filePath = Path.Combine(
            _outputDirectory,
            $"{_filePrefix}_{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}.parquet");

        WriteFileAsync(filePath, events).ConfigureAwait(false).GetAwaiter().GetResult();
        _catalog.CommitFileAsync(filePath, events.Count, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private async System.Threading.Tasks.Task WriteFileAsync(
        string filePath,
        IReadOnlyList<LogEvent> events)
    {
        // Fully qualified: this sink lives under the Herald.Sinks namespace,
        // so the sibling Herald.Sinks.File namespace shadows a bare `File`.
        // System.IO.File pins the BCL type unambiguously.
        await using var stream = System.IO.File.Create(filePath);
        using var writer = await ParquetWriter.CreateAsync(_schema, stream).ConfigureAwait(false);
        using var rowGroup = writer.CreateRowGroup();

        var times = new DateTime[events.Count];
        var levels = new string[events.Count];
        var categories = new string[events.Count];
        var messages = new string[events.Count];
        var templates = new string[events.Count];
        var exceptions = new string?[events.Count];
        var properties = new string?[events.Count];

        for (var i = 0; i < events.Count; i++)
        {
            var evt = events[i];
            times[i] = evt.TimeUtc.UtcDateTime;
            levels[i] = evt.Level.Key;
            categories[i] = evt.Category.Value;
            messages[i] = evt.Message;
            templates[i] = evt.MessageTemplate;
            exceptions[i] = GetExceptionText(evt);
            properties[i] = GetPropertiesJson(evt);
        }

        var fields = _schema.DataFields;
        await rowGroup.WriteColumnAsync(new DataColumn(fields[0], times)).ConfigureAwait(false);
        await rowGroup.WriteColumnAsync(new DataColumn(fields[1], levels)).ConfigureAwait(false);
        await rowGroup.WriteColumnAsync(new DataColumn(fields[2], categories)).ConfigureAwait(false);
        await rowGroup.WriteColumnAsync(new DataColumn(fields[3], messages)).ConfigureAwait(false);
        await rowGroup.WriteColumnAsync(new DataColumn(fields[4], templates)).ConfigureAwait(false);
        await rowGroup.WriteColumnAsync(new DataColumn(fields[5], exceptions)).ConfigureAwait(false);
        await rowGroup.WriteColumnAsync(new DataColumn(fields[6], properties)).ConfigureAwait(false);
    }

    private static ParquetSchema BuildSchema() => new(
        new DataField<DateTime>("time_utc"),
        new DataField<string>("level"),
        new DataField<string>("category"),
        new DataField<string>("message"),
        new DataField<string>("template"),
        new DataField<string?>("exception"),
        new DataField<string?>("properties"));

    private static string? GetExceptionText(LogEvent evt)
    {
        if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value) && value is Exception ex)
            return ex.ToString();
        return null;
    }

    private static string? GetPropertiesJson(LogEvent evt)
    {
        if (evt.Properties is null || evt.Properties.Count == 0) return null;

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var prop in evt.Properties)
            {
                writer.WriteString(prop.Name, prop.ResolvedValue?.ToString());
            }
            writer.WriteEndObject();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
