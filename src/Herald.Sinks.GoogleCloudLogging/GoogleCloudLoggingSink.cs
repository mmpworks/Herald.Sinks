// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using Google.Api;
using Google.Cloud.Logging.Type;
using Google.Cloud.Logging.V2;
using Google.Protobuf.WellKnownTypes;
using MMP.Herald;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;
// google.logging.v2 ships a LogEntry type; alias Herald's LogEvent so
// every bare `LogEvent` reference below resolves to Herald's.
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.GoogleCloudLogging;

/// <summary>
/// Sink that writes log events to Google Cloud Logging (formerly
/// Stackdriver) via <c>LoggingServiceV2Client.WriteLogEntries</c>.
/// Drop-in for <c>Serilog.Sinks.GoogleCloudLogging</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Auth.</b> Uses Application Default Credentials (ADC) — set
/// <c>GOOGLE_APPLICATION_CREDENTIALS</c> to a service-account JSON, or
/// run under compute with a service-account attached. The code-first
/// ctor accepts a pre-built <see cref="LoggingServiceV2Client"/> for
/// callers that already own one.
/// </para>
/// <para>
/// <b>Resource.</b> Each log entry carries a
/// <see cref="MonitoredResource"/> labelling the source (GCE instance,
/// GKE container, Cloud Run service, etc.). The sink defaults to
/// <c>global</c> — sufficient for on-prem or local dev; production
/// workloads should override with the actual runtime resource.
/// </para>
/// <para>
/// <b>Severity mapping.</b> Trace → Debug, Debug → Debug, Info →
/// Info, Notice/Success → Notice, Warn → Warning, Error → Error,
/// Critical → Critical, Security → Alert. Unknown levels → Default.
/// Maps to Cloud Logging's standard severity enum so the log viewer
/// colour-codes consistently.
/// </para>
/// </remarks>
public sealed class GoogleCloudLoggingSink : ILogger, IBatchedLogSink
{
    private readonly LoggingServiceV2Client _client;
    private readonly LogName _logName;
    private readonly MonitoredResource _resource;

    public GoogleCloudLoggingSink(
        string projectId,
        string logId = "herald",
        MonitoredResource? resource = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(logId);

        _client = LoggingServiceV2Client.Create();
        _logName = new LogName(projectId, logId);
        _resource = resource ?? new MonitoredResource { Type = "global" };
    }

    /// <summary>
    /// Code-first overload for callers that already own a
    /// <see cref="LoggingServiceV2Client"/> — typical when the app
    /// shares a client across services.
    /// </summary>
    public GoogleCloudLoggingSink(
        LoggingServiceV2Client client,
        string projectId,
        string logId = "herald",
        MonitoredResource? resource = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(logId);

        _client = client;
        _logName = new LogName(projectId, logId);
        _resource = resource ?? new MonitoredResource { Type = "global" };
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

        var entries = new LogEntry[events.Count];
        for (var i = 0; i < events.Count; i++)
        {
            entries[i] = BuildEntry(events[i]);
        }

        // WriteLogEntries accepts a per-request LogName and Resource
        // that apply to every entry whose own LogName / Resource are
        // unset. Saves payload size on the common case where every
        // event targets the same log.
        _client.WriteLogEntriesAsync(_logName, _resource, labels: null, entries: entries)
            .GetAwaiter().GetResult();
    }

    private LogEntry BuildEntry(LogEvent evt)
    {
        var entry = new LogEntry
        {
            LogName = _logName.ToString(),
            Resource = _resource,
            Severity = MapSeverity(evt.Level.Key),
            Timestamp = Timestamp.FromDateTime(evt.TimeUtc.UtcDateTime),
            TextPayload = evt.Message ?? string.Empty,
        };

        entry.Labels.Add("category", evt.Category.Value);
        entry.Labels.Add("template", evt.MessageTemplate ?? string.Empty);

        if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value) && value is Exception ex)
        {
            entry.Labels.Add("exception_type", ex.GetType().FullName ?? ex.GetType().Name);
            entry.Labels.Add("exception", TruncateForLabel(ex.ToString()));
        }

        if (evt.Properties is not null && evt.Properties.Count > 0)
        {
            foreach (var prop in evt.Properties)
            {
                // Cloud Logging labels cap at 1024 chars per value.
                // Longer values truncate; structured values beyond that
                // belong in the jsonPayload path (follow-up feature).
                var stringValue = prop.ResolvedValue?.ToString();
                if (stringValue is not null)
                {
                    entry.Labels.Add(prop.Name, TruncateForLabel(stringValue));
                }
            }
        }

        return entry;
    }

    private static LogSeverity MapSeverity(string levelKey) => levelKey switch
    {
        "trace" or "debug" => LogSeverity.Debug,
        "info" => LogSeverity.Info,
        "notice" or "success" => LogSeverity.Notice,
        "warn" => LogSeverity.Warning,
        "error" => LogSeverity.Error,
        "critical" => LogSeverity.Critical,
        "security" => LogSeverity.Alert,
        _ => LogSeverity.Default,
    };

    private static string TruncateForLabel(string value) =>
        value.Length <= 1024 ? value : value[..1024];
}
