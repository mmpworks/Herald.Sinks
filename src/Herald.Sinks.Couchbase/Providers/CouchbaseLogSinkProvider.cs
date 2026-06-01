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
using MMP.Herald.Sinks.Batching;

namespace Herald.Sinks.Couchbase.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="CouchbaseLogSink"/> from
/// a <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// <para>
/// Configuration sources, in priority order:
/// <list type="bullet">
///   <item><b>v2 property bag</b> (<c>configuration-couchbase.mmpform</c> → <see cref="LoggingRuntimeSinkDefinition.Properties"/>):
///     <list type="bullet">
///       <item><c>connection_string</c> — cluster URI (required).</item>
///       <item><c>username</c> — RBAC username (required).</item>
///       <item><c>password</c> — RBAC password (required).</item>
///       <item><c>bucket</c> — bucket name (required).</item>
///       <item><c>scope</c> — scope name (default <c>_default</c>).</item>
///       <item><c>collection</c> — collection name (default <c>_default</c>).</item>
///     </list>
///   </item>
///   <item><b>Legacy slots</b> (forward-compatibility — Couchbase had
///         no legacy JSON-config path before the v2 bag):
///     <list type="bullet">
///       <item><see cref="LoggingRuntimeSinkDefinition.Uri"/> ↔ connection_string.</item>
///       <item><see cref="LoggingRuntimeSinkDefinition.Alias"/> ↔ password.</item>
///       <item><see cref="LoggingRuntimeSinkDefinition.Host"/> ↔ bucket.</item>
///     </list>
///     <c>username</c>, <c>scope</c>, and <c>collection</c> have no
///     legacy mapping; older deployments used the code-first ctor.
///   </item>
/// </list>
/// The prior provider threw <see cref="NotSupportedException"/> because
/// the credential surface could not ride safely through the three
/// legacy string slots. The v2 bag fixes that.
/// </para>
/// </remarks>
public sealed class CouchbaseLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "couchbase";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var resolved = CouchbaseSinkRuntimeConfig.From(definition);

        if (string.IsNullOrWhiteSpace(resolved.ConnectionString))
        {
            throw new ArgumentException(
                $"Couchbase sink '{definition.Name}' is missing the required 'connection_string' property " +
                $"(or legacy Uri). Set it in configuration-couchbase.mmpform.",
                nameof(definition));
        }
        if (string.IsNullOrWhiteSpace(resolved.Username))
        {
            throw new ArgumentException(
                $"Couchbase sink '{definition.Name}' is missing the required 'username' property. " +
                $"Set it in configuration-couchbase.mmpform.",
                nameof(definition));
        }
        if (string.IsNullOrWhiteSpace(resolved.Password))
        {
            throw new ArgumentException(
                $"Couchbase sink '{definition.Name}' is missing the required 'password' property " +
                $"(or legacy Alias). Set it in configuration-couchbase.mmpform.",
                nameof(definition));
        }
        if (string.IsNullOrWhiteSpace(resolved.Bucket))
        {
            throw new ArgumentException(
                $"Couchbase sink '{definition.Name}' is missing the required 'bucket' property " +
                $"(or legacy Host). Set it in configuration-couchbase.mmpform.",
                nameof(definition));
        }

        var sink = new CouchbaseLogSink(
            connectionString: resolved.ConnectionString!,
            username: resolved.Username!,
            password: resolved.Password!,
            bucketName: resolved.Bucket!,
            scopeName: resolved.Scope,
            collectionName: resolved.Collection);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }
}
