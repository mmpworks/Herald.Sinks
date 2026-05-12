// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using MMP.Herald;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;
// AWS SDK ships its own LogEvent type in Amazon.CloudWatchLogs.Model.
// Alias Herald's LogEvent so every bare `LogEvent` reference below
// resolves unambiguously to ours. InputLogEvent (the AWS ingest type)
// stays under its original name.
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.AwsCloudWatch;

/// <summary>
/// Sink that writes log events to an AWS CloudWatch Logs log group /
/// log stream via <c>PutLogEvents</c>. Drop-in for
/// <c>Serilog.Sinks.AwsCloudWatch</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Auth.</b> Uses the AWS SDK's default credential resolution chain
/// (environment, shared credentials file, IMDS, SSO, role assumption)
/// unless <c>AmazonCloudWatchLogsClient</c> is supplied via the
/// code-first ctor. Production deployments typically rely on IAM roles
/// attached to the compute platform.
/// </para>
/// <para>
/// <b>Auto-create.</b> When <c>autoCreateLogGroup</c> or
/// <c>autoCreateLogStream</c> is true the sink issues the
/// corresponding <c>CreateLogGroup</c> / <c>CreateLogStream</c> on
/// first write. Preferred shape in production is installer-managed
/// groups; the flags exist for dev loops.
/// </para>
/// <para>
/// <b>Batching.</b> Implements <see cref="IBatchedLogSink"/> so the
/// pipeline's batching step packs events into <c>PutLogEvents</c>
/// calls. CloudWatch caps each call at 10,000 events or 1MB; events
/// larger than the 1MB payload ceiling truncate silently rather than
/// failing the batch.
/// </para>
/// </remarks>
public sealed class AwsCloudWatchLogSink : ILogger, IBatchedLogSink, IDisposable
{
    private const int MaxBatchEvents = 10_000;
    private const int MaxEventBytes = 262_144;  // 256 KB single-event ceiling

    private readonly IAmazonCloudWatchLogs _client;
    private readonly bool _ownsClient;
    private readonly string _logGroupName;
    private readonly string _logStreamName;
    private readonly bool _autoCreateLogGroup;
    private readonly bool _autoCreateLogStream;
    private int _resourcesEnsured;

    public AwsCloudWatchLogSink(
        string logGroupName,
        string logStreamName,
        string? regionSystemName = null,
        bool autoCreateLogGroup = false,
        bool autoCreateLogStream = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logGroupName);
        ArgumentException.ThrowIfNullOrWhiteSpace(logStreamName);

        _logGroupName = logGroupName;
        _logStreamName = logStreamName;
        _autoCreateLogGroup = autoCreateLogGroup;
        _autoCreateLogStream = autoCreateLogStream;

        _client = regionSystemName is null
            ? new AmazonCloudWatchLogsClient()
            : new AmazonCloudWatchLogsClient(RegionEndpoint.GetBySystemName(regionSystemName));
        _ownsClient = true;
    }

    /// <summary>
    /// Code-first overload for callers that already own an
    /// <see cref="IAmazonCloudWatchLogs"/> — typical when the app
    /// shares an AWS SDK client across repositories.
    /// </summary>
    public AwsCloudWatchLogSink(
        IAmazonCloudWatchLogs client,
        string logGroupName,
        string logStreamName,
        bool autoCreateLogGroup = false,
        bool autoCreateLogStream = false)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(logGroupName);
        ArgumentException.ThrowIfNullOrWhiteSpace(logStreamName);

        _client = client;
        _ownsClient = false;
        _logGroupName = logGroupName;
        _logStreamName = logStreamName;
        _autoCreateLogGroup = autoCreateLogGroup;
        _autoCreateLogStream = autoCreateLogStream;
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
        EnsureResourcesOnce();

        // CloudWatch requires events in chronological order per call.
        // Pipeline batching preserves arrival order; we trust that but
        // stable-sort as defense against out-of-order feeds.
        var ordered = events.OrderBy(e => e.TimeUtc.ToUnixTimeMilliseconds()).ToList();

        for (var offset = 0; offset < ordered.Count; offset += MaxBatchEvents)
        {
            var chunkSize = Math.Min(MaxBatchEvents, ordered.Count - offset);
            var inputEvents = new List<InputLogEvent>(chunkSize);
            for (var i = 0; i < chunkSize; i++)
            {
                inputEvents.Add(BuildInputEvent(ordered[offset + i]));
            }

            var request = new PutLogEventsRequest
            {
                LogGroupName = _logGroupName,
                LogStreamName = _logStreamName,
                LogEvents = inputEvents,
            };

            _client.PutLogEventsAsync(request).GetAwaiter().GetResult();
        }
    }

    public void Dispose()
    {
        if (_ownsClient) _client.Dispose();
    }

    private static InputLogEvent BuildInputEvent(LogEvent evt)
    {
        var message = BuildJson(evt);
        if (Encoding.UTF8.GetByteCount(message) > MaxEventBytes)
        {
            // Truncate to fit under the single-event ceiling rather
            // than failing the whole batch on one oversized event.
            message = message[..Math.Min(MaxEventBytes / 4, message.Length)] + "\"...truncated\"";
        }

        return new InputLogEvent
        {
            Timestamp = evt.TimeUtc.UtcDateTime,
            Message = message,
        };
    }

    private static string BuildJson(LogEvent evt)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("level", evt.Level.Key);
            writer.WriteString("category", evt.Category.Value);
            writer.WriteString("message", evt.Message ?? string.Empty);
            writer.WriteString("template", evt.MessageTemplate ?? string.Empty);

            if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value) && value is Exception ex)
            {
                writer.WriteString("exception", ex.ToString());
                writer.WriteString("exception_type", ex.GetType().FullName ?? ex.GetType().Name);
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
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private void EnsureResourcesOnce()
    {
        if (!_autoCreateLogGroup && !_autoCreateLogStream) return;
        if (System.Threading.Interlocked.Exchange(ref _resourcesEnsured, 1) == 1) return;

        if (_autoCreateLogGroup)
        {
            try
            {
                _client.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = _logGroupName })
                    .GetAwaiter().GetResult();
            }
            catch (ResourceAlreadyExistsException)
            {
                // Idempotent: someone else got here first. Safe.
            }
        }

        if (_autoCreateLogStream)
        {
            try
            {
                _client.CreateLogStreamAsync(new CreateLogStreamRequest
                {
                    LogGroupName = _logGroupName,
                    LogStreamName = _logStreamName,
                }).GetAwaiter().GetResult();
            }
            catch (ResourceAlreadyExistsException)
            {
                // Idempotent as above.
            }
        }
    }
}
