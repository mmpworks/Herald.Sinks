// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using MMP.Herald.Configuration.Runtime;

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
    private const string KeyBaseUrl     = "base_url";
    private const string KeyIndexPrefix = "index_prefix";
    private const string KeyUsername    = "username";
    private const string KeyPassword    = "password";
    private const string KeyApiKey      = "api_key";

    /// <summary>Default index prefix — matches the sink ctor default.</summary>
    public const string DefaultIndexPrefix = "herald-logs";

    /// <summary>
    /// Resolved Elasticsearch sink config. <see cref="BaseUrl"/>
    /// stays nullable so the provider can fail with a named
    /// ArgumentException; <see cref="IndexPrefix"/> carries its
    /// default. Auth fields are independently nullable; the sink's
    /// own resolver picks API key over Basic when both arrive.
    /// </summary>
    public readonly record struct Resolved(
        string? BaseUrl,
        string IndexPrefix,
        string? Username,
        string? Password,
        string? ApiKey);

    public static Resolved From(LoggingRuntimeSinkDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var bag = definition.Properties;
        return new Resolved(
            BaseUrl:     ReadString(bag, KeyBaseUrl)     ?? Nullify(definition.Uri),
            IndexPrefix: ReadString(bag, KeyIndexPrefix) ?? DefaultIndexPrefix,
            Username:    ReadString(bag, KeyUsername),
            Password:    ReadString(bag, KeyPassword),
            ApiKey:      ReadString(bag, KeyApiKey));
    }

    private static string? ReadString(
        IReadOnlyDictionary<string, object?>? bag, string key)
    {
        if (bag is null) return null;
        if (!bag.TryGetValue(key, out var raw) || raw is null) return null;
        var text = raw.ToString();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static string? Nullify(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
