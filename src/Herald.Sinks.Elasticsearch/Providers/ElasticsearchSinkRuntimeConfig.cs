// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Configuration.Sinks;

namespace Herald.Sinks.Elasticsearch.Providers;

/// <summary>
/// Maps a <see cref="LoggingRuntimeSinkDefinition"/> into the values
/// <see cref="ElasticsearchLogSink"/>'s constructor needs.
///
/// <para>
/// The Elasticsearch mmpform exposes five fields:
/// <list type="bullet">
///   <item><c>base_url</c> — cluster base URL (required).</item>
///   <item><c>index_prefix</c> — index prefix (default
///         <c>herald-logs</c>; daily indices land at
///         <c>{prefix}-yyyy.MM.dd</c>).</item>
///   <item><c>username</c> — HTTP Basic auth username.</item>
///   <item><c>password</c> — HTTP Basic auth password.</item>
///   <item><c>api_key</c> — alternative auth (Authorization: ApiKey).</item>
/// </list>
/// Older deployments authored before the v2 bag had no auth path at
/// all — the sink ctor accepted no credential parameters. The
/// legacy slot fallback only carries <c>base_url</c> from
/// <c>Uri</c>; auth is bag-only because no legacy field was used.
/// </para>
/// </summary>
internal static class ElasticsearchSinkRuntimeConfig
{
    private const string KeyBaseUrl      = "base_url";
    private const string KeyIndexPrefix  = "index_prefix";
    private const string KeyUsername     = "username";
    private const string KeyPassword     = "password";
    private const string KeyApiKey       = "api_key";
    private const string KeySchema       = "schema";
    private const string KeyEcsVersion   = "ecs_version";
    private const string KeyEventDataset = "event_dataset";

    /// <summary>Default index prefix — matches the sink ctor default.</summary>
    public const string DefaultIndexPrefix = "herald-logs";

    /// <summary>Default schema token — native keeps today's shape.</summary>
    public const string DefaultSchema = "native";

    /// <summary>
    /// Resolved Elasticsearch sink config. <see cref="BaseUrl"/>
    /// stays nullable so the provider can fail with a named
    /// ArgumentException; <see cref="IndexPrefix"/> carries its
    /// default. Auth fields are independently nullable; the sink's
    /// own resolver picks API key over Basic when both arrive.
    /// <see cref="Schema"/> is the raw token (native/ecs) — the provider
    /// resolves it to the enum so it can throw a named, value-listing
    /// exception on an unknown value. <see cref="EcsVersion"/> /
    /// <see cref="EventDataset"/> are used only in ECS mode.
    /// </summary>
    public readonly record struct Resolved(
        string? BaseUrl,
        string IndexPrefix,
        string? Username,
        string? Password,
        string? ApiKey,
        string Schema,
        string? EcsVersion,
        string? EventDataset);

    public static Resolved From(LoggingRuntimeSinkDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var bag = definition.Properties;
        return new Resolved(
            BaseUrl:      SinkPropertyBag.ReadString(bag, KeyBaseUrl)     ?? SinkPropertyBag.Nullify(definition.Uri),
            IndexPrefix:  SinkPropertyBag.ReadString(bag, KeyIndexPrefix) ?? DefaultIndexPrefix,
            Username:     SinkPropertyBag.ReadString(bag, KeyUsername),
            Password:     SinkPropertyBag.ReadString(bag, KeyPassword),
            ApiKey:       SinkPropertyBag.ReadString(bag, KeyApiKey),
            Schema:       SinkPropertyBag.ReadString(bag, KeySchema) ?? DefaultSchema,
            EcsVersion:   SinkPropertyBag.ReadString(bag, KeyEcsVersion),
            EventDataset: SinkPropertyBag.ReadString(bag, KeyEventDataset));
    }
}
