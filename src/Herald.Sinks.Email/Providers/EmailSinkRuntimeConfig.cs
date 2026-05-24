// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Configuration.Sinks;

namespace Herald.Sinks.Email.Providers;

/// <summary>
/// Maps a <see cref="LoggingRuntimeSinkDefinition"/> into the values
/// <see cref="EmailLogSink"/>'s connection-string constructor needs.
///
/// <para>
/// The Email mmpform exposes eight operator-facing fields:
/// <list type="bullet">
///   <item><c>smtp_host</c> — SMTP server hostname (required).</item>
///   <item><c>smtp_port</c> — SMTP server port (required, default 587).</item>
///   <item><c>from_address</c> — From address (required).</item>
///   <item><c>to_addresses</c> — comma-separated recipient list (required).</item>
///   <item><c>username</c> — optional SMTP auth username.</item>
///   <item><c>password</c> — optional SMTP auth password.</item>
///   <item><c>use_start_tls</c> — STARTTLS toggle (default true).</item>
///   <item><c>subject_template</c> — subject line template.</item>
/// </list>
/// The previous provider threw <see cref="NotSupportedException"/>
/// because the credential-and-recipient surface did not fit the
/// three legacy slots. The v2 bag carries it cleanly now.
/// </para>
/// </summary>
internal static class EmailSinkRuntimeConfig
{
    private const string KeySmtpHost        = "smtp_host";
    private const string KeySmtpPort        = "smtp_port";
    private const string KeyFromAddress     = "from_address";
    private const string KeyToAddresses     = "to_addresses";
    private const string KeyUsername        = "username";
    private const string KeyPassword        = "password";
    private const string KeyUseStartTls     = "use_start_tls";
    private const string KeySubjectTemplate = "subject_template";

    /// <summary>SMTP submission port (RFC 6409) — STARTTLS default.</summary>
    public const int DefaultSmtpPort = 587;

    /// <summary>Default subject template — matches the sink ctor default.</summary>
    public const string DefaultSubjectTemplate = "[Herald {level}] log alert";

    /// <summary>
    /// Resolved Email sink config. Required fields stay nullable so
    /// the provider can fail with a single field-named
    /// ArgumentException; recipients always carry a (possibly empty)
    /// list so the constructor argument is non-null.
    /// </summary>
    public readonly record struct Resolved(
        string? SmtpHost,
        int SmtpPort,
        string? FromAddress,
        IReadOnlyList<string> ToAddresses,
        string? Username,
        string? Password,
        bool UseStartTls,
        string SubjectTemplate);

    public static Resolved From(LoggingRuntimeSinkDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var bag = definition.Properties;

        var smtpHost = SinkPropertyBag.ReadString(bag, KeySmtpHost) ?? SinkPropertyBag.Nullify(definition.Uri);
        var smtpPort = SinkPropertyBag.ReadInt(bag, KeySmtpPort) ?? DefaultSmtpPort;
        var fromAddress = SinkPropertyBag.ReadString(bag, KeyFromAddress) ?? SinkPropertyBag.Nullify(definition.Host);
        var toAddresses = ParseRecipients(SinkPropertyBag.ReadString(bag, KeyToAddresses));
        var username = SinkPropertyBag.ReadString(bag, KeyUsername);
        var password = SinkPropertyBag.ReadString(bag, KeyPassword) ?? SinkPropertyBag.Nullify(definition.Alias);
        var useStartTls = SinkPropertyBag.ReadBool(bag, KeyUseStartTls) ?? true;
        var subjectTemplate = SinkPropertyBag.ReadString(bag, KeySubjectTemplate) ?? DefaultSubjectTemplate;

        return new Resolved(
            smtpHost, smtpPort, fromAddress, toAddresses,
            username, password, useStartTls, subjectTemplate);
    }

    // The mmpform exposes recipients as a single comma-separated
    // string because the dashboard's textfield only supports scalars
    // today. Splitting here keeps that contract local — the sink
    // constructor still takes IEnumerable<string>.
    //
    // Cannot use SinkPropertyBag.ReadKeyValuePairs because recipients
    // are bare values without "key=" prefix. Per Richard's Pass-3
    // recipe step 8, sink-specific parsers (this one, Mqtt's
    // ParseBroker, Syslog's ParseLegacyAlias) stay local because
    // they are not duplicated across sinks.
    private static IReadOnlyList<string> ParseRecipients(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return Array.Empty<string>();

        var result = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            if (!string.IsNullOrWhiteSpace(part)) result.Add(part);
        }
        return result;
    }
}
