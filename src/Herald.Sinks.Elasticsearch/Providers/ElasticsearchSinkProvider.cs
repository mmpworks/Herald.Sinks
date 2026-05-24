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

namespace Herald.Sinks.Elasticsearch.Providers;

/// <summary>
/// Sink provider for Elasticsearch via the Bulk API.
/// </summary>
/// <remarks>
/// <para>
/// Configuration sources, in priority order:
/// <list type="bullet">
///   <item><b>v2 property bag</b> (<c>configuration-elasticsearch.mmpform</c> → <see cref="LoggingRuntimeSinkDefinition.Properties"/>):
///     <list type="bullet">
///       <item><c>base_url</c> — cluster base URL (required).</item>
///       <item><c>index_prefix</c> — index prefix (default <c>herald-logs</c>).</item>
///       <item><c>username</c> — HTTP Basic auth username.</item>
///       <item><c>password</c> — HTTP Basic auth password.</item>
///       <item><c>api_key</c> — alternative auth via <c>Authorization: ApiKey</c>.</item>
///     </list>
///   </item>
///   <item><b>Legacy slot</b>:
///     <list type="bullet">
///       <item><see cref="LoggingRuntimeSinkDefinition.Uri"/> ↔ base_url.</item>
///     </list>
///     Auth had no legacy slot — the sink ctor did not accept
///     credentials before this change. Older deployments either
///     fronted Elasticsearch with an auth proxy or accepted that the
///     cluster was open.
///   </item>
/// </list>
/// API key beats Basic when both are populated.
/// </para>
/// </remarks>
public sealed class ElasticsearchSinkProvider : ILogSinkProvider
{
    public const string KindKey = "elasticsearch";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => ElasticsearchLogSink.MinEdition;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var resolved = ElasticsearchSinkRuntimeConfig.From(definition);

        if (string.IsNullOrWhiteSpace(resolved.BaseUrl))
        {
            throw new ArgumentException(
                $"Elasticsearch sink '{definition.Name}' is missing the required 'base_url' property " +
                $"(or legacy Uri). Set it in configuration-elasticsearch.mmpform.",
                nameof(definition));
        }

        return new ElasticsearchLogSink(
            baseUrl: resolved.BaseUrl!,
            levelRegistry: levelRegistry,
            indexPrefix: resolved.IndexPrefix,
            username: resolved.Username,
            password: resolved.Password,
            apiKey: resolved.ApiKey);
    }
}
