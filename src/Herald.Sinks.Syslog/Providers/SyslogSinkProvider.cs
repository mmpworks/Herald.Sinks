// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using MMP.Herald;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Levels;
using MMP.Herald.Output.Rendering;
using MMP.Herald.Pipeline;
using MMP.Herald.Routing;

namespace Herald.Sinks.Syslog.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="SyslogSink"/> from a
/// <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// <para>
/// Configuration sources, in priority order:
/// <list type="bullet">
///   <item><b>v2 property bag</b> (<c>configuration-syslog.mmpform</c> → <see cref="LoggingRuntimeSinkDefinition.Properties"/>):
///     <list type="bullet">
///       <item><c>host</c> — collector hostname (required).</item>
///       <item><c>port</c> — collector port (default 514).</item>
///       <item><c>transport</c> — <c>udp</c> or <c>tcp</c>.</item>
///       <item><c>format</c> — <c>rfc5424</c> or <c>rfc3164</c>.</item>
///       <item><c>facility</c> — <c>user</c>, <c>daemon</c>,
///             <c>local0..local7</c>, <c>auth</c>, ... Unknown values
///             fall back to <c>user</c>.</item>
///       <item><c>app_name</c>, <c>process_id</c>, <c>log_source_host</c>
///             — RFC 5424 header fields. Each falls through to its
///             sink-side default when blank.</item>
///       <item><c>structured_data_id</c> — SD-ID for the RFC 5424
///             structured-data block (default <c>herald@32473</c>).</item>
///       <item><c>structured_data_enabled</c> — toggle SD emission
///             (default true). The toggle is the BLOCKER fix: when
///             enabled, Herald event properties land in the SD block
///             instead of dropping on the wire.</item>
///     </list>
///   </item>
///   <item><b>Legacy slots</b> (pre-v2 dashboard JSON):
///     <list type="bullet">
///       <item><see cref="LoggingRuntimeSinkDefinition.Uri"/> ↔ host.</item>
///       <item><see cref="LoggingRuntimeSinkDefinition.Host"/> ↔ port
///             (parsed as int; defaults to 514 when blank or
///             unparseable).</item>
///       <item><see cref="LoggingRuntimeSinkDefinition.Alias"/> ↔
///             pipe-delimited transport + format switches
///             (e.g. <c>"udp|rfc5424"</c>, <c>"tcp|rfc3164"</c>).</item>
///     </list>
///     The other seven keys had no legacy slot — operators on pre-v2
///     dashboards relied on the sink ctor defaults or used the
///     code-first ctor.
///   </item>
/// </list>
/// TLS (RFC 5425 syslog-over-TLS) belongs to the Compliance-edition
/// TLS sub-track and is intentionally not configured here.
/// </para>
/// </remarks>
public sealed class SyslogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "syslog";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var resolved = SyslogSinkRuntimeConfig.From(definition);

        if (string.IsNullOrWhiteSpace(resolved.Host))
        {
            throw new ArgumentException(
                $"Syslog sink '{definition.Name}' is missing the required 'host' property " +
                $"(or legacy Uri). Set it in configuration-syslog.mmpform.",
                nameof(definition));
        }

        return new SyslogSink(
            host: resolved.Host!,
            port: resolved.Port,
            format: resolved.Format,
            transport: resolved.Transport,
            facility: resolved.Facility,
            logSourceHost: resolved.LogSourceHost,
            appName: resolved.AppName,
            processId: resolved.ProcessId,
            structuredDataId: resolved.StructuredDataId,
            structuredDataEnabled: resolved.StructuredDataEnabled);
    }
}
