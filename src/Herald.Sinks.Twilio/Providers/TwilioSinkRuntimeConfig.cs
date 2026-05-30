// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Configuration.Sinks;

namespace Herald.Sinks.Twilio.Providers;

/// <summary>
/// Maps a <see cref="LoggingRuntimeSinkDefinition"/> into the values
/// <see cref="TwilioLogSink"/>'s constructor needs.
///
/// <para>
/// The Twilio mmpform exposes four required fields:
/// <list type="bullet">
///   <item><c>account_sid</c> — Twilio Account SID.</item>
///   <item><c>auth_token</c> — Twilio Auth Token.</item>
///   <item><c>from_number</c> — Twilio-provisioned sending number.</item>
///   <item><c>to_number</c> — destination number.</item>
/// </list>
/// The previous provider threw <see cref="NotSupportedException"/>
/// because the four-field surface did not fit the three legacy slots.
/// The v2 bag carries it cleanly now.
/// </para>
/// </summary>
internal static class TwilioSinkRuntimeConfig
{
    private const string KeyAccountSid = "account_sid";
    private const string KeyAuthToken  = "auth_token";
    private const string KeyFromNumber = "from_number";
    private const string KeyToNumber   = "to_number";

    /// <summary>
    /// Resolved Twilio sink config. Required fields stay nullable so
    /// the provider can fail with a single field-named
    /// ArgumentException.
    /// </summary>
    public readonly record struct Resolved(
        string? AccountSid,
        string? AuthToken,
        string? FromNumber,
        string? ToNumber);

    public static Resolved From(LoggingRuntimeSinkDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var bag = definition.Properties;
        return new Resolved(
            AccountSid: SinkPropertyBag.ReadString(bag, KeyAccountSid) ?? SinkPropertyBag.Nullify(definition.Uri),
            AuthToken:  SinkPropertyBag.ReadString(bag, KeyAuthToken)  ?? SinkPropertyBag.Nullify(definition.Alias),
            FromNumber: SinkPropertyBag.ReadString(bag, KeyFromNumber) ?? SinkPropertyBag.Nullify(definition.Host),
            ToNumber:   SinkPropertyBag.ReadString(bag, KeyToNumber));
    }
}
