// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Globalization;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Configuration.Sinks;

namespace Herald.Sinks.Syslog.Providers;

/// <summary>
/// Maps a <see cref="LoggingRuntimeSinkDefinition"/> into the values
/// <see cref="SyslogSink"/>'s constructor needs.
///
/// <para>
/// The mmpform exposes ten operator-facing fields:
/// <list type="bullet">
///   <item><c>host</c> — collector hostname (required).</item>
///   <item><c>port</c> — collector port (default 514).</item>
///   <item><c>transport</c> — <c>udp</c> or <c>tcp</c>.</item>
///   <item><c>format</c> — <c>rfc5424</c> or <c>rfc3164</c>.</item>
///   <item><c>facility</c> — <c>user</c>, <c>daemon</c>, <c>local0..local7</c>, etc.</item>
///   <item><c>app_name</c>, <c>process_id</c>, <c>log_source_host</c>
///         — RFC 5424 header fields.</item>
///   <item><c>structured_data_id</c> — SD-ID for the RFC 5424
///         structured-data block (default <c>herald@32473</c>).</item>
///   <item><c>structured_data_enabled</c> — toggle SD emission
///         (default true).</item>
/// </list>
/// Older deployments populated <c>host</c> via <c>Uri</c>, <c>port</c>
/// via <c>Host</c> (parsed as int), and packed transport + format into
/// <c>Alias</c> as pipe-delimited tokens (<c>"udp|rfc5424"</c>). The
/// legacy fallback preserves those readings.
/// </para>
///
/// <para>
/// Per Richard's audit (BLOCKER for Syslog): RFC 5424 STRUCTURED-DATA
/// was hardcoded to NILVALUE, which silently dropped every Herald
/// property on the wire. This mapper plus the SyslogMessageBuilder
/// change wires properties into the SD-ELEMENT.
/// </para>
/// </summary>
internal static class SyslogSinkRuntimeConfig
{
    private const string KeyHost                  = "host";
    private const string KeyPort                  = "port";
    private const string KeyTransport             = "transport";
    private const string KeyFormat                = "format";
    private const string KeyFacility              = "facility";
    private const string KeyAppName               = "app_name";
    private const string KeyProcessId             = "process_id";
    private const string KeyLogSourceHost         = "log_source_host";
    private const string KeyStructuredDataId      = "structured_data_id";
    private const string KeyStructuredDataEnabled = "structured_data_enabled";

    /// <summary>Default syslog port — used for both UDP and TCP.</summary>
    public const int DefaultPort = 514;

    /// <summary>Default SD-ID — matches the sink ctor default.</summary>
    public const string DefaultStructuredDataId = "herald@32473";

    /// <summary>
    /// Resolved Syslog sink config. <see cref="Host"/> stays nullable
    /// so the provider can fail with a named ArgumentException; every
    /// other field carries a typed default.
    /// </summary>
    public readonly record struct Resolved(
        string? Host,
        int Port,
        SyslogTransport Transport,
        SyslogFormat Format,
        SyslogFacility Facility,
        string? AppName,
        string? ProcessId,
        string? LogSourceHost,
        string StructuredDataId,
        bool StructuredDataEnabled);

    public static Resolved From(LoggingRuntimeSinkDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var bag = definition.Properties;
        var (legacyTransport, legacyFormat) = ParseLegacyAlias(definition.Alias);

        // Transport / Format / Facility all collapse via SinkPropertyBag.ReadEnum<T>:
        //   - mmpform vocabulary (`udp`, `tcp`, `rfc5424`, `rfc3164`,
        //     `local0`, `authpriv`, etc.) matches the enum member names
        //     case-insensitively, which is exactly what
        //     Enum.TryParse<T>(ignoreCase: true) accepts.
        //   - Unknown bag values return null; the `??` operator falls
        //     through to the legacy alias parse (Transport / Format) or
        //     the User default (Facility), preserving the prior
        //     "unknown → safe default" semantics.
        return new Resolved(
            Host:                  SinkPropertyBag.ReadString(bag, KeyHost) ?? SinkPropertyBag.Nullify(definition.Uri),
            Port:                  SinkPropertyBag.ReadInt(bag, KeyPort) ?? ParseLegacyPort(definition.Host),
            Transport:             SinkPropertyBag.ReadEnum<SyslogTransport>(bag, KeyTransport) ?? legacyTransport,
            Format:                SinkPropertyBag.ReadEnum<SyslogFormat>(bag, KeyFormat) ?? legacyFormat,
            Facility:              SinkPropertyBag.ReadEnum<SyslogFacility>(bag, KeyFacility) ?? SyslogFacility.User,
            AppName:               SinkPropertyBag.ReadString(bag, KeyAppName),
            ProcessId:             SinkPropertyBag.ReadString(bag, KeyProcessId),
            LogSourceHost:         SinkPropertyBag.ReadString(bag, KeyLogSourceHost),
            StructuredDataId:      SinkPropertyBag.ReadString(bag, KeyStructuredDataId) ?? DefaultStructuredDataId,
            StructuredDataEnabled: SinkPropertyBag.ReadBool(bag, KeyStructuredDataEnabled) ?? true);
    }

    // Legacy port lived in definition.Host as an integer string — the
    // old provider parsed it with int.TryParse and fell back to 514.
    // Stays local because it reads from a raw string slot, not from
    // the bag. SinkPropertyBag has no "parse-string-int-or-default"
    // primitive (and shouldn't — the bag-vs-slot distinction is the
    // whole reason Nullify and ReadString are separate primitives).
    private static int ParseLegacyPort(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return DefaultPort;
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
            ? port
            : DefaultPort;
    }

    // Legacy alias was a pipe-delimited switches string like
    // "udp|rfc5424". Match the old provider's parser shape so legacy
    // deployments don't break. Stays local because it parses two
    // independent enum values out of one string — no single
    // SinkPropertyBag primitive fits that shape.
    private static (SyslogTransport Transport, SyslogFormat Format) ParseLegacyAlias(string? alias)
    {
        var transport = SyslogTransport.Udp;
        var format = SyslogFormat.Rfc5424;
        if (string.IsNullOrWhiteSpace(alias)) return (transport, format);

        foreach (var raw in alias.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "udp":     transport = SyslogTransport.Udp; break;
                case "tcp":     transport = SyslogTransport.Tcp; break;
                case "rfc5424": format = SyslogFormat.Rfc5424; break;
                case "rfc3164": format = SyslogFormat.Rfc3164; break;
            }
        }
        return (transport, format);
    }
}
