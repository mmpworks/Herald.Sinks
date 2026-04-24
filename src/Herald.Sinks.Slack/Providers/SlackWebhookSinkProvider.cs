// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using MMP.Herald;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Levels;
using MMP.Herald.Output.Rendering;
using MMP.Herald.Pipeline;
using MMP.Herald.Routing;

namespace Herald.Sinks.Slack.Providers;

/// <summary>
/// Sink provider for Slack incoming webhooks.
/// </summary>
public sealed class SlackWebhookSinkProvider : ILogSinkProvider
{
    public const string KindKey = "slack";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => SlackWebhookLogSink.MinEdition;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);
        return new SlackWebhookLogSink(definition.Uri, levelRegistry);
    }
}
