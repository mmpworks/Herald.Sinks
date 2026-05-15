// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.ApplicationInsightsSdk;

/// <summary>
/// Sink that tracks Herald log events as <see cref="TraceTelemetry"/>
/// and <see cref="ExceptionTelemetry"/> via the Application Insights
/// <see cref="TelemetryClient"/>. Drop-in for
/// Serilog.Sinks.ApplicationInsights.
/// </summary>
/// <remarks>
/// <para>
/// <b>Telemetry types.</b> Events with a <c>$exception</c> context
/// entry track as <c>ExceptionTelemetry</c>; everything else tracks as
/// <c>TraceTelemetry</c>. The level maps to a <see cref="SeverityLevel"/>
/// so AI's filtering and alerting work without further config.
/// </para>
/// <para>
/// <b>Connection string vs instrumentation key.</b> AI now prefers
/// connection strings (multi-region routing). The constructor accepts
/// a connection string; legacy instrumentation-key-only setups can
/// pass <c>InstrumentationKey={key}</c>.
/// </para>
/// </remarks>
public sealed class ApplicationInsightsLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable, INetworkSink
{
    private readonly TelemetryClient _client;
    private readonly TelemetryConfiguration? _ownedConfig;

    public ApplicationInsightsLogSink(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _ownedConfig = new TelemetryConfiguration { ConnectionString = connectionString };
        _client = new TelemetryClient(_ownedConfig);
    }

    /// <summary>
    /// Code-first overload accepts a pre-built <see cref="TelemetryClient"/>
    /// — typical when the host already has Application Insights wired
    /// through DI. The sink does not dispose the client because it
    /// does not own the underlying configuration.
    /// </summary>
    public ApplicationInsightsLogSink(TelemetryClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _ownedConfig = null;
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        // Exception events promote to ExceptionTelemetry so AI's
        // exception-tab analytics light up. Everything else is a trace.
        if (TryGetException(logEvent, out var ex) && ex is not null)
        {
            var telemetry = new ExceptionTelemetry(ex)
            {
                SeverityLevel = MapSeverity(logEvent.Level.Key),
                Message = logEvent.Message,
            };
            CopyProperties(logEvent, telemetry.Properties);
            _client.TrackException(telemetry);
            return;
        }

        var trace = new TraceTelemetry(logEvent.Message ?? string.Empty, MapSeverity(logEvent.Level.Key));
        CopyProperties(logEvent, trace.Properties);
        _client.TrackTrace(trace);
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;
        // TelemetryClient batches and ships internally; per-event Track*
        // calls feed the ingestion buffer. A Flush at the end of the
        // batch pushes anything that's still in the buffer so the call
        // doesn't return before the events are on the wire.
        foreach (var evt in events) Log(evt);
        _client.Flush();
    }

    public void Dispose()
    {
        _client.Flush();
        _ownedConfig?.Dispose();
    }

    private static SeverityLevel MapSeverity(string levelKey) => levelKey switch
    {
        "trace" => SeverityLevel.Verbose,
        "debug" => SeverityLevel.Verbose,
        "info" => SeverityLevel.Information,
        "notice" => SeverityLevel.Information,
        "warn" => SeverityLevel.Warning,
        "error" => SeverityLevel.Error,
        "critical" => SeverityLevel.Critical,
        "security" => SeverityLevel.Critical,
        _ => SeverityLevel.Information,
    };

    private static bool TryGetException(LogEvent evt, out Exception? exception)
    {
        if (evt.Context is not null
            && evt.Context.TryGetValue("$exception", out var raw)
            && raw is Exception ex)
        {
            exception = ex;
            return true;
        }
        exception = null;
        return false;
    }

    private static void CopyProperties(LogEvent evt, IDictionary<string, string> bag)
    {
        bag["category"] = evt.Category.Value;
        bag["template"] = evt.MessageTemplate ?? string.Empty;
        if (evt.Properties is not null)
        {
            foreach (var prop in evt.Properties)
            {
                bag[prop.Name] = prop.ResolvedValue?.ToString() ?? string.Empty;
            }
        }
    }
}
