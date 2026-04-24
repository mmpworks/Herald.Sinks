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

namespace Herald.Sinks.SignalFx.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="SignalFxLogSink"/> from
/// a <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// Wire-up conventions:
/// <list type="bullet">
///   <item><c>Alias</c> holds the SignalFx access token (<c>X-SF-Token</c>).</item>
///   <item><c>Host</c>, when set, is treated as the realm name (e.g. <c>us1</c>).</item>
///   <item><c>Uri</c> overrides the endpoint entirely — used to target
///   on-prem Splunk Observability or a test intake.</item>
/// </list>
/// Dimensions aren't surfaced through JSON config in this first pass;
/// callers that need them pass a pre-built <see cref="SignalFxLogSink"/>
/// via <c>WithCustomSinkProvider</c>.
/// </remarks>
public sealed class SignalFxLogSinkProvider : ILogSinkProvider
{
    /// <summary>
    /// The sink-kind string that identifies this provider in JSON config.
    /// Local to the provider — the Herald.Sinks monorepo keeps each
    /// sink's identifier with its source, so Core no longer owns the
    /// constant.
    /// </summary>
    public const string KindKey = "signalfx";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Enterprise;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Alias);

        return new SignalFxLogSink(
            accessToken: definition.Alias,
            endpoint: definition.Uri,
            realm: definition.Host);
    }
}
