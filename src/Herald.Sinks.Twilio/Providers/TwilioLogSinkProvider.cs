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

namespace Herald.Sinks.Twilio.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="TwilioLogSink"/> from
/// a <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// <para>
/// Configuration sources, in priority order:
/// <list type="bullet">
///   <item><b>v2 property bag</b> (<c>configuration-twilio.mmpform</c> → <see cref="LoggingRuntimeSinkDefinition.Properties"/>):
///     <list type="bullet">
///       <item><c>account_sid</c> — Twilio Account SID (required).</item>
///       <item><c>auth_token</c> — Twilio Auth Token (required).</item>
///       <item><c>from_number</c> — Twilio-provisioned sender (required).</item>
///       <item><c>to_number</c> — destination number (required).</item>
///     </list>
///   </item>
///   <item><b>Legacy slots</b> (forward-compatibility — Twilio had no
///         legacy JSON-config path before the v2 bag):
///     <list type="bullet">
///       <item><see cref="LoggingRuntimeSinkDefinition.Uri"/> ↔ account_sid.</item>
///       <item><see cref="LoggingRuntimeSinkDefinition.Alias"/> ↔ auth_token.</item>
///       <item><see cref="LoggingRuntimeSinkDefinition.Host"/> ↔ from_number.</item>
///     </list>
///     <c>to_number</c> has no legacy mapping; older deployments used
///     the code-first ctor.
///   </item>
/// </list>
/// </para>
/// </remarks>
public sealed class TwilioLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "twilio";
    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var resolved = TwilioSinkRuntimeConfig.From(definition);

        if (string.IsNullOrWhiteSpace(resolved.AccountSid))
        {
            throw new ArgumentException(
                $"Twilio sink '{definition.Name}' is missing the required 'account_sid' property " +
                $"(or legacy Uri). Set it in configuration-twilio.mmpform.",
                nameof(definition));
        }
        if (string.IsNullOrWhiteSpace(resolved.AuthToken))
        {
            throw new ArgumentException(
                $"Twilio sink '{definition.Name}' is missing the required 'auth_token' property " +
                $"(or legacy Alias). Set it in configuration-twilio.mmpform.",
                nameof(definition));
        }
        if (string.IsNullOrWhiteSpace(resolved.FromNumber))
        {
            throw new ArgumentException(
                $"Twilio sink '{definition.Name}' is missing the required 'from_number' property " +
                $"(or legacy Host). Set it in configuration-twilio.mmpform.",
                nameof(definition));
        }
        if (string.IsNullOrWhiteSpace(resolved.ToNumber))
        {
            throw new ArgumentException(
                $"Twilio sink '{definition.Name}' is missing the required 'to_number' property. " +
                $"Set it in configuration-twilio.mmpform.",
                nameof(definition));
        }

        var sink = new TwilioLogSink(
            accountSid: resolved.AccountSid!,
            authToken: resolved.AuthToken!,
            fromNumber: resolved.FromNumber!,
            toNumber: resolved.ToNumber!);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }
}
