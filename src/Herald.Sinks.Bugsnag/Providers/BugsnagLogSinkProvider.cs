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

namespace Herald.Sinks.Bugsnag.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="BugsnagLogSink"/> from
/// a <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// <para>
/// Configuration sources, in priority order:
/// <list type="bullet">
///   <item><b>v2 property bag</b> (<c>configuration-bugsnag.mmpform</c> → <see cref="LoggingRuntimeSinkDefinition.Properties"/>):
///     <list type="bullet">
///       <item><c>api_key</c> — Bugsnag project API key (required).</item>
///       <item><c>endpoint</c> — notify endpoint.</item>
///       <item><c>release_stage</c> — release stage tag for dashboard filtering.</item>
///     </list>
///   </item>
///   <item><b>Legacy slot</b>:
///     <list type="bullet">
///       <item><see cref="LoggingRuntimeSinkDefinition.Uri"/> ↔ api_key
///             (the pre-v2 path put the key here).</item>
///     </list>
///     <c>endpoint</c> and <c>release_stage</c> have no legacy mapping;
///     older deployments could only set them via the code-first ctor.
///   </item>
/// </list>
/// </para>
/// </remarks>
public sealed class BugsnagLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "bugsnag";
    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var resolved = BugsnagSinkRuntimeConfig.From(definition);

        if (string.IsNullOrWhiteSpace(resolved.ApiKey))
        {
            throw new ArgumentException(
                $"Bugsnag sink '{definition.Name}' is missing the required 'api_key' property " +
                $"(or legacy Uri). Set it in configuration-bugsnag.mmpform.",
                nameof(definition));
        }

        return new BugsnagLogSink(
            apiKey: resolved.ApiKey!,
            endpoint: resolved.Endpoint,
            releaseStage: resolved.ReleaseStage);
    }
}
