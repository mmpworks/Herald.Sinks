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

namespace Herald.Sinks.GenericWebhook.Providers;

/// <summary>
/// Sink provider for the generic webhook with optional rules engine.
/// Rules are injected at construction and forwarded to the sink instance.
/// </summary>
public sealed class GenericWebhookSinkProvider : ILogSinkProvider
{
    public const string KindKey = "webhook";

    private readonly IReadOnlyList<WebhookRule>? _rules;

    public GenericWebhookSinkProvider(IReadOnlyList<WebhookRule>? rules = null)
    {
        _rules = rules;
    }

    public string SinkKind => KindKey;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);
        return new GenericWebhookLogSink(definition.Uri, levelRegistry, rules: _rules);
    }
}
