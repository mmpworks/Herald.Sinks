// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using MMP.Herald;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Levels;
using MMP.Herald.Output.Rendering;
using MMP.Herald.Pipeline;
using MMP.Herald.Routing;

namespace Herald.Sinks.EventLog.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="EventLogSink"/> from a
/// <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// Wire-up:
/// <list type="bullet">
///   <item><c>Alias</c> is the Event Source name (required).</item>
///   <item><c>Host</c> is the Log name, default <c>Application</c>.</item>
///   <item><c>Uri</c> is the target Machine name, default local.</item>
/// </list>
/// <c>autoCreateSource</c> is not surfaced through JSON — source
/// creation requires admin and should go through the installer, not the
/// config file.
/// </remarks>
public sealed class EventLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "event_log";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Alias);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException(
                "EventLogSink is Windows-only. Remove the 'event_log' sink from the " +
                "non-Windows config or use Herald.Sinks.Syslog as a cross-platform " +
                "alternative.");
        }

        return CreateWindowsSink(definition);
    }

    [SupportedOSPlatform("windows")]
    private static ILogger CreateWindowsSink(LoggingRuntimeSinkDefinition definition) =>
        new EventLogSink(
            source: definition.Alias!,
            logName: string.IsNullOrWhiteSpace(definition.Host) ? "Application" : definition.Host,
            machineName: definition.Uri);
}
