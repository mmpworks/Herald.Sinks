// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.AzureBlobStorage;

/// <summary>
/// Sink that uploads Herald log events as NDJSON blobs to an Azure
/// Blob Storage container. Drop-in for Serilog.Sinks.AzureBlobStorage.
/// One LogBatch produces one blob; key layout is date-partitioned for
/// listable retention.
/// </summary>
/// <remarks>
/// <para>
/// <b>Auth.</b> Two paths: connection string (account key embedded) or
/// DefaultAzureCredential against a blob endpoint URL. Production
/// workloads should prefer the credential path with a managed identity.
/// </para>
/// <para>
/// <b>Key layout.</b>
/// <c>{prefix}/{yyyy-MM-dd}/{HHmmss-ffff}-{guid}.log.jsonl</c>. Date
/// partitioning keeps the container listable; the millisecond + GUID
/// tail prevents collisions under concurrent flushes.
/// </para>
/// </remarks>
public sealed class AzureBlobStorageLogSink : HeraldSinkBase, IBatchedLogSink
{
    private readonly BlobContainerClient _container;
    private readonly string _keyPrefix;

    public AzureBlobStorageLogSink(
        string connectionString,
        string containerName,
        string keyPrefix = "logs")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentNullException.ThrowIfNull(keyPrefix);

        _container = new BlobContainerClient(connectionString, containerName);
        _keyPrefix = keyPrefix.TrimEnd('/');
    }

    /// <summary>
    /// Code-first overload using DefaultAzureCredential against an
    /// account endpoint (e.g.
    /// <c>https://acct.blob.core.windows.net</c>).
    /// </summary>
    public AzureBlobStorageLogSink(
        Uri accountEndpoint,
        string containerName,
        string keyPrefix = "logs")
    {
        ArgumentNullException.ThrowIfNull(accountEndpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentNullException.ThrowIfNull(keyPrefix);

        var serviceClient = new BlobServiceClient(accountEndpoint, new DefaultAzureCredential());
        _container = serviceClient.GetBlobContainerClient(containerName);
        _keyPrefix = keyPrefix.TrimEnd('/');
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        LogBatch(new[] { logEvent });
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        var body = BuildBody(events);
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var now = DateTime.UtcNow;
        var key = $"{_keyPrefix}/{now:yyyy-MM-dd}/{now:HHmmss-ffff}-{Guid.NewGuid():N}.log.jsonl";

        var blob = _container.GetBlobClient(key);
        using var stream = new MemoryStream(bodyBytes);
        blob.Upload(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "application/x-ndjson" },
        });
    }

    private static string BuildBody(IReadOnlyList<LogEvent> events)
    {
        var sb = new StringBuilder();
        foreach (var evt in events)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("time_utc", evt.TimeUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
                writer.WriteString("level", evt.Level.Key);
                writer.WriteString("category", evt.Category.Value);
                writer.WriteString("message", evt.Message ?? string.Empty);
                writer.WriteString("template", evt.MessageTemplate ?? string.Empty);

                if (evt.Context.TryGetValue(LogContextKeys.Exception, out var v) && v is Exception ex)
                    writer.WriteString("exception", ex.ToString());

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
            sb.Append(Encoding.UTF8.GetString(stream.ToArray())).Append('\n');
        }
        return sb.ToString();
    }
}
