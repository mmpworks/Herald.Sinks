// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using MMP.Herald.Configuration.Runtime;

namespace Herald.Sinks.Couchbase.Providers;

/// <summary>
/// Maps a <see cref="LoggingRuntimeSinkDefinition"/> into the values
/// <see cref="CouchbaseLogSink"/>'s connection-string constructor
/// needs.
///
/// <para>
/// The Couchbase mmpform exposes six operator-facing fields:
/// <list type="bullet">
///   <item><c>connection_string</c> — cluster URI (required).</item>
///   <item><c>username</c> — RBAC username (required).</item>
///   <item><c>password</c> — RBAC password (required).</item>
///   <item><c>bucket</c> — bucket name (required).</item>
///   <item><c>scope</c> — scope name (default <c>_default</c>).</item>
///   <item><c>collection</c> — collection name (default <c>_default</c>).</item>
/// </list>
/// The previous provider threw <see cref="NotSupportedException"/> for
/// exactly this credential-rich surface. The v2 bag carries it
/// cleanly now.
/// </para>
/// </summary>
internal static class CouchbaseSinkRuntimeConfig
{
    private const string KeyConnectionString = "connection_string";
    private const string KeyUsername         = "username";
    private const string KeyPassword         = "password";
    private const string KeyBucket           = "bucket";
    private const string KeyScope            = "scope";
    private const string KeyCollection       = "collection";

    /// <summary>Couchbase SDK default scope/collection name.</summary>
    public const string DefaultScope = "_default";

    /// <summary>Couchbase SDK default scope/collection name.</summary>
    public const string DefaultCollection = "_default";

    /// <summary>
    /// Resolved Couchbase sink config. Required fields stay nullable
    /// so the provider can fail with a single field-named
    /// ArgumentException; scope and collection always carry their
    /// defaults so the constructor signature is satisfied either way.
    /// </summary>
    public readonly record struct Resolved(
        string? ConnectionString,
        string? Username,
        string? Password,
        string? Bucket,
        string Scope,
        string Collection);

    public static Resolved From(LoggingRuntimeSinkDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var bag = definition.Properties;

        // Bag-first, then legacy fallback. Three of six fields have
        // a sensible legacy slot mapping (Uri/Alias/Host →
        // connection_string/password/bucket); username, scope, and
        // collection only ever came from the bag.
        var connectionString = ReadString(bag, KeyConnectionString) ?? Nullify(definition.Uri);
        var username = ReadString(bag, KeyUsername);
        var password = ReadString(bag, KeyPassword) ?? Nullify(definition.Alias);
        var bucket = ReadString(bag, KeyBucket) ?? Nullify(definition.Host);
        var scope = ReadString(bag, KeyScope) ?? DefaultScope;
        var collection = ReadString(bag, KeyCollection) ?? DefaultCollection;

        return new Resolved(connectionString, username, password, bucket, scope, collection);
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
