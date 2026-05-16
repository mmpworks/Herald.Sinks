// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Globalization;
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
/// Wire-up:
/// <list type="bullet">
///   <item><c>Uri</c> → target host (required).</item>
///   <item><c>Host</c> → optional port override (integer). Default 514.</item>
///   <item><c>Alias</c> → optional transport/format switch as
///   <c>"udp|rfc5424"</c>, <c>"tcp|rfc3164"</c>, etc. Case-insensitive.
///   Defaults to <c>udp|rfc5424</c>.</item>
/// </list>
/// Facility, app name, process id, and source host fall back to
/// sensible defaults — callers who need non-defaults construct the
/// sink directly via <c>WithCustomSinkProvider</c>.
/// </remarks>
public sealed class SyslogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "syslog";

    public string SinkKind => KindKey;
    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);

        var port = 514;
        if (!string.IsNullOrWhiteSpace(definition.Host)
            && int.TryParse(definition.Host, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPort))
        {
            port = parsedPort;
        }

        var (transport, format) = ParseAlias(definition.Alias);

        return new SyslogSink(
            host: definition.Uri,
            port: port,
            format: format,
            transport: transport);
    }

    private static (SyslogTransport Transport, SyslogFormat Format) ParseAlias(string? alias)
    {
        // Default shape when alias is absent.
        var transport = SyslogTransport.Udp;
        var format = SyslogFormat.Rfc5424;

        if (string.IsNullOrWhiteSpace(alias)) return (transport, format);

        foreach (var raw in alias.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var token = raw.ToLowerInvariant();
            switch (token)
            {
                case "udp": transport = SyslogTransport.Udp; break;
                case "tcp": transport = SyslogTransport.Tcp; break;
                case "rfc5424": format = SyslogFormat.Rfc5424; break;
                case "rfc3164": format = SyslogFormat.Rfc3164; break;
                // Unknown tokens are ignored — forward-compatible for
                // future alias switches.
            }
        }

        return (transport, format);
    }
}
