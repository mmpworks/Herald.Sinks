// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using MMP.Herald.Configuration.Runtime;

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

        return new Resolved(
            Host:                  ReadString(bag, KeyHost) ?? Nullify(definition.Uri),
            Port:                  ReadInt(bag, KeyPort) ?? ParseLegacyPort(definition.Host),
            Transport:             ParseTransport(ReadString(bag, KeyTransport), legacyTransport),
            Format:                ParseFormat(ReadString(bag, KeyFormat), legacyFormat),
            Facility:              ParseFacility(ReadString(bag, KeyFacility)),
            AppName:               ReadString(bag, KeyAppName),
            ProcessId:             ReadString(bag, KeyProcessId),
            LogSourceHost:         ReadString(bag, KeyLogSourceHost),
            StructuredDataId:      ReadString(bag, KeyStructuredDataId) ?? DefaultStructuredDataId,
            StructuredDataEnabled: ReadBool(bag, KeyStructuredDataEnabled) ?? true);
    }

    // Transport parser: bag value wins, then legacy alias token,
    // then default UDP.
    private static SyslogTransport ParseTransport(string? raw, SyslogTransport legacy) =>
        (raw ?? "").Trim().ToLowerInvariant() switch
        {
            "tcp" => SyslogTransport.Tcp,
            "udp" => SyslogTransport.Udp,
            ""    => legacy,
            _     => legacy,
        };

    // Format parser: bag value wins, then legacy alias token, then RFC 5424.
    private static SyslogFormat ParseFormat(string? raw, SyslogFormat legacy) =>
        (raw ?? "").Trim().ToLowerInvariant() switch
        {
            "rfc5424" => SyslogFormat.Rfc5424,
            "rfc3164" => SyslogFormat.Rfc3164,
            ""        => legacy,
            _         => legacy,
        };

    // Facility parser: lowercase string → enum value. Unknown values
    // fall back to User — the default for application logs.
    private static SyslogFacility ParseFacility(string? raw) =>
        (raw ?? "").Trim().ToLowerInvariant() switch
        {
            "kernel"       => SyslogFacility.Kernel,
            "user"         => SyslogFacility.User,
            "mail"         => SyslogFacility.Mail,
            "daemon"       => SyslogFacility.Daemon,
            "auth"         => SyslogFacility.Auth,
            "syslog"       => SyslogFacility.Syslog,
            "lpr"          => SyslogFacility.Lpr,
            "news"         => SyslogFacility.News,
            "uucp"         => SyslogFacility.Uucp,
            "cron"         => SyslogFacility.Cron,
            "authpriv"     => SyslogFacility.AuthPriv,
            "ftp"          => SyslogFacility.Ftp,
            "ntp"          => SyslogFacility.Ntp,
            "audit"        => SyslogFacility.Audit,
            "alert"        => SyslogFacility.Alert,
            "clockdaemon"  => SyslogFacility.ClockDaemon,
            "local0"       => SyslogFacility.Local0,
            "local1"       => SyslogFacility.Local1,
            "local2"       => SyslogFacility.Local2,
            "local3"       => SyslogFacility.Local3,
            "local4"       => SyslogFacility.Local4,
            "local5"       => SyslogFacility.Local5,
            "local6"       => SyslogFacility.Local6,
            "local7"       => SyslogFacility.Local7,
            _              => SyslogFacility.User,
        };

    // Legacy port lived in definition.Host as an integer string — the
    // old provider parsed it with int.TryParse and fell back to 514.
    // Keep that semantics for deployments still carrying the old form.
    private static int ParseLegacyPort(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return DefaultPort;
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
            ? port
            : DefaultPort;
    }

    // Legacy alias was a pipe-delimited switches string like
    // "udp|rfc5424". Match the old provider's parser shape so legacy
    // deployments don't break.
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

    private static string? ReadString(
        IReadOnlyDictionary<string, object?>? bag, string key)
    {
        if (bag is null) return null;
        if (!bag.TryGetValue(key, out var raw) || raw is null) return null;
        var text = raw.ToString();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static bool? ReadBool(IReadOnlyDictionary<string, object?>? bag, string key)
    {
        if (bag is null) return null;
        if (!bag.TryGetValue(key, out var raw) || raw is null) return null;
        if (raw is bool b) return b;
        return bool.TryParse(raw.ToString(), out var parsed) ? parsed : (bool?)null;
    }

    private static int? ReadInt(IReadOnlyDictionary<string, object?>? bag, string key)
    {
        if (bag is null) return null;
        if (!bag.TryGetValue(key, out var raw) || raw is null) return null;
        return raw switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
            _ => (int?)null
        };
    }

    private static string? Nullify(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
