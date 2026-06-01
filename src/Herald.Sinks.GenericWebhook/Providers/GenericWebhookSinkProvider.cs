// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using MMP.Herald;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Levels;
using MMP.Herald.Output.Rendering;
using MMP.Herald.Pipeline;
using MMP.Herald.Routing;
using MMP.Herald.Sinks.Batching;

namespace Herald.Sinks.GenericWebhook.Providers;

/// <summary>
/// Sink provider for the generic webhook with optional rules engine.
/// </summary>
/// <remarks>
/// <para>
/// Configuration sources, in priority order:
/// <list type="bullet">
///   <item><b>v2 property bag</b> (<c>configuration-webhook.mmpform</c> → <see cref="LoggingRuntimeSinkDefinition.Properties"/>):
///     <list type="bullet">
///       <item><c>url</c> — webhook URL (required).</item>
///       <item><c>headers</c> — comma-separated <c>Name=Value</c> pairs.</item>
///       <item><c>content_type</c> — Content-Type for the POST body.</item>
///     </list>
///   </item>
///   <item><b>Legacy slot</b>: <see cref="LoggingRuntimeSinkDefinition.Uri"/> ↔ url.
///         Headers and content_type had no legacy mapping — operators
///         needing custom headers had to construct the sink in code.</item>
/// </list>
/// </para>
/// <para>
/// Rules are still injected at construction (via the provider's own
/// ctor) and forwarded to the sink. JSON-config serialisation of
/// rules is a separate piece of work — bag-driven rules need a
/// nested-array form-shape the dashboard doesn't render yet.
/// </para>
/// </remarks>
public sealed class GenericWebhookSinkProvider : BatchingSinkProviderBase
{
    public const string KindKey = "webhook";

    private readonly IReadOnlyList<WebhookRule>? _rules;

    public GenericWebhookSinkProvider(IReadOnlyList<WebhookRule>? rules = null)
    {
        _rules = rules;
    }

    public override string SinkKind => KindKey;
    public override HeraldEdition MinimumEdition => GenericWebhookLogSink.MinEdition;

    public override ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var resolved = GenericWebhookSinkRuntimeConfig.From(definition);

        if (string.IsNullOrWhiteSpace(resolved.Url))
        {
            throw new ArgumentException(
                $"GenericWebhook sink '{definition.Name}' is missing the required 'url' property " +
                $"(or legacy Uri). Set it in configuration-webhook.mmpform.",
                nameof(definition));
        }

        var sink = new GenericWebhookLogSink(
            url: resolved.Url!,
            levelRegistry: levelRegistry,
            headers: resolved.Headers,
            contentType: resolved.ContentType,
            rules: _rules);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }
}
