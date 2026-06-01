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
using MMP.Herald.Sinks.Batching;

namespace Herald.Sinks.Email.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="EmailLogSink"/> from a
/// <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// <para>
/// Configuration sources, in priority order:
/// <list type="bullet">
///   <item><b>v2 property bag</b> (<c>configuration-email.mmpform</c> → <see cref="LoggingRuntimeSinkDefinition.Properties"/>):
///     <list type="bullet">
///       <item><c>smtp_host</c> — SMTP server hostname (required).</item>
///       <item><c>smtp_port</c> — SMTP server port (required, default 587).</item>
///       <item><c>from_address</c> — From address (required).</item>
///       <item><c>to_addresses</c> — comma-separated recipient list (required).</item>
///       <item><c>username</c> — optional SMTP auth username.</item>
///       <item><c>password</c> — optional SMTP auth password.</item>
///       <item><c>use_start_tls</c> — STARTTLS toggle (default true).</item>
///       <item><c>subject_template</c> — subject line template
///             (default <c>[Herald {level}] log alert</c>).</item>
///     </list>
///   </item>
///   <item><b>Legacy slots</b> (forward-compatibility — Email had no
///         legacy JSON-config path before the v2 bag):
///     <list type="bullet">
///       <item><see cref="LoggingRuntimeSinkDefinition.Uri"/> ↔ smtp_host.</item>
///       <item><see cref="LoggingRuntimeSinkDefinition.Host"/> ↔ from_address.</item>
///       <item><see cref="LoggingRuntimeSinkDefinition.Alias"/> ↔ password.</item>
///     </list>
///     <c>smtp_port</c>, <c>to_addresses</c>, <c>username</c>,
///     <c>use_start_tls</c>, and <c>subject_template</c> have no
///     legacy mapping; older deployments used the code-first ctor.
///   </item>
/// </list>
/// </para>
/// </remarks>
public sealed class EmailLogSinkProvider : BatchingSinkProviderBase
{
    public const string KindKey = "email";

    public override string SinkKind => KindKey;
    public override HeraldEdition MinimumEdition => HeraldEdition.Community;

    public override ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var resolved = EmailSinkRuntimeConfig.From(definition);

        if (string.IsNullOrWhiteSpace(resolved.SmtpHost))
        {
            throw new ArgumentException(
                $"Email sink '{definition.Name}' is missing the required 'smtp_host' property " +
                $"(or legacy Uri). Set it in configuration-email.mmpform.",
                nameof(definition));
        }
        if (string.IsNullOrWhiteSpace(resolved.FromAddress))
        {
            throw new ArgumentException(
                $"Email sink '{definition.Name}' is missing the required 'from_address' property " +
                $"(or legacy Host). Set it in configuration-email.mmpform.",
                nameof(definition));
        }
        if (resolved.ToAddresses.Count == 0)
        {
            throw new ArgumentException(
                $"Email sink '{definition.Name}' is missing the required 'to_addresses' property — " +
                $"supply a comma-separated list of recipient addresses in configuration-email.mmpform.",
                nameof(definition));
        }

        var sink = new EmailLogSink(
            smtpHost: resolved.SmtpHost!,
            smtpPort: resolved.SmtpPort,
            fromAddress: resolved.FromAddress!,
            toAddresses: resolved.ToAddresses,
            username: resolved.Username,
            password: resolved.Password,
            useStartTls: resolved.UseStartTls,
            subjectTemplate: resolved.SubjectTemplate);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }
}
