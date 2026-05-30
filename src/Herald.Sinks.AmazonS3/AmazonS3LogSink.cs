// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;
// AWS SDK doesn't shadow Herald's LogEvent, but we alias for consistency.
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.AmazonS3;

/// <summary>
/// Sink that uploads Herald log events as NDJSON objects to an
/// Amazon S3 bucket. Drop-in for Serilog.Sinks.AmazonS3. Each
/// <c>LogBatch</c> call produces one S3 object — pair with the
/// pipeline's batching step for sensible object sizing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Key format.</b> <c>{prefix}/{yyyy-MM-dd}/{HHmmss-ffff}-{guid}.log.jsonl</c>.
/// Date partitioning keeps the bucket listable; the millisecond + GUID
/// tail prevents collisions under concurrent flushes.
/// </para>
/// <para>
/// <b>Auth.</b> AWS SDK default credential chain — env vars, shared
/// credentials file, IMDS, SSO, role. Production workloads typically
/// rely on an IAM role attached to the compute platform.
/// </para>
/// </remarks>
public sealed class AmazonS3LogSink : HeraldSinkBase, IBatchedLogSink, IDisposable, INetworkSink
{
    private readonly IAmazonS3 _client;
    private readonly bool _ownsClient;
    private readonly string _bucketName;
    private readonly string _keyPrefix;

    public AmazonS3LogSink(
        string bucketName,
        string keyPrefix = "logs",
        string? regionSystemName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        ArgumentNullException.ThrowIfNull(keyPrefix);

        _bucketName = bucketName;
        _keyPrefix = keyPrefix.TrimEnd('/');
        _client = regionSystemName is null
            ? new AmazonS3Client()
            : new AmazonS3Client(RegionEndpoint.GetBySystemName(regionSystemName));
        _ownsClient = true;
    }

    /// <summary>
    /// Code-first overload for callers that already own an
    /// <see cref="IAmazonS3"/> — typical when the app shares an AWS SDK
    /// client across services.
    /// </summary>
    public AmazonS3LogSink(
        IAmazonS3 client,
        string bucketName,
        string keyPrefix = "logs")
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        ArgumentNullException.ThrowIfNull(keyPrefix);

        _client = client;
        _ownsClient = false;
        _bucketName = bucketName;
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

        using var stream = new MemoryStream(bodyBytes);
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = stream,
            ContentType = "application/x-ndjson",
        };

        _client.PutObjectAsync(request).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        if (_ownsClient) _client.Dispose();
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

                if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value) && value is Exception ex)
                {
                    writer.WriteString("exception", ex.ToString());
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
            sb.Append(Encoding.UTF8.GetString(stream.ToArray()));
            sb.Append('\n');
        }
        return sb.ToString();
    }
}
