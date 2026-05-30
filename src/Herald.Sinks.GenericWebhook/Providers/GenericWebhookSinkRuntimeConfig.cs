// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Configuration.Sinks;

namespace Herald.Sinks.GenericWebhook.Providers;

/// <summary>
/// Maps a <see cref="LoggingRuntimeSinkDefinition"/> into the values
/// <see cref="GenericWebhookLogSink"/>'s constructor needs.
///
/// <para>
/// The mmpform exposes three fields:
/// <list type="bullet">
///   <item><c>url</c> — webhook URL (required).</item>
///   <item><c>headers</c> — comma-separated <c>Name=Value</c> pairs
///         that ride on every outgoing request. The key path for
///         Bearer tokens, API keys, tenant tags, and signed-request
///         schemes.</item>
///   <item><c>content_type</c> — Content-Type header for the POST
///         body (default <c>application/json</c>).</item>
/// </list>
/// Per Richard's audit (BLOCKER for GenericWebhook): the prior
/// provider passed only <c>Uri</c> and dropped headers on the floor,
/// which made the sink useless against any endpoint that required
/// auth. Surfacing headers through the bag closes the BLOCKER.
/// </para>
///
/// <para>
/// Rules engine config (the <c>rules</c> ctor arg) stays code-first
/// via <see cref="GenericWebhookSinkProvider"/>'s ctor — operators
/// who need rule-driven dispatch register the provider with a
/// rules list at composition time. JSON-config serialisation of
/// rules is a separate piece of work.
/// </para>
/// </summary>
internal static class GenericWebhookSinkRuntimeConfig
{
    private const string KeyUrl         = "url";
    private const string KeyHeaders     = "headers";
    private const string KeyContentType = "content_type";

    /// <summary>Default Content-Type — matches the sink ctor default.</summary>
    public const string DefaultContentType = "application/json";

    /// <summary>
    /// Resolved GenericWebhook sink config. <see cref="Url"/> stays
    /// nullable so the provider can fail with a named
    /// ArgumentException. <see cref="ContentType"/> always carries
    /// a value (default <c>application/json</c>).
    /// </summary>
    public readonly record struct Resolved(
        string? Url,
        IReadOnlyDictionary<string, string>? Headers,
        string ContentType);

    public static Resolved From(LoggingRuntimeSinkDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var bag = definition.Properties;
        return new Resolved(
            Url:         SinkPropertyBag.ReadString(bag, KeyUrl)         ?? SinkPropertyBag.Nullify(definition.Uri),
            Headers:     SinkPropertyBag.ReadKeyValuePairs(bag, KeyHeaders),
            ContentType: SinkPropertyBag.ReadString(bag, KeyContentType) ?? DefaultContentType);
    }
}
