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

namespace Herald.Sinks.PagerDuty.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="PagerDutyLogSink"/> from
/// a <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
public sealed class PagerDutyLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "pagerduty";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Enterprise;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Alias);

        return new PagerDutyLogSink(
            routingKey: definition.Alias,
            source: definition.Host,
            endpoint: definition.Uri);
    }
}
