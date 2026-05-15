// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using Couchbase;
using Couchbase.KeyValue;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.Couchbase;

/// <summary>
/// Sink that upserts log events as JSON documents into a Couchbase
/// bucket / scope / collection via CouchbaseNetClient. Drop-in for
/// Serilog.Sinks.Couchbase.
/// </summary>
/// <remarks>
/// <para>
/// <b>Document shape.</b> Each event is upserted under a composite key
/// (<c>date#inverted-ticks#rand</c>) so newest documents sort first
/// within a date prefix. Operators query via N1QL against the
/// Level / Category / TimeUtc fields.
/// </para>
/// <para>
/// <b>Auth.</b> The connection-string ctor builds a Cluster on
/// construction; the code-first overload accepts a pre-built
/// <see cref="ICouchbaseCollection"/> for apps that already share a
/// cluster instance.
/// </para>
/// <para>
/// <b>Thread safety.</b> CouchbaseNetClient cluster + collection
/// instances are thread-safe per the SDK contract.
/// </para>
/// </remarks>
public sealed class CouchbaseLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable, INetworkSink
{
    private readonly ICouchbaseCollection _collection;
    private readonly ICluster? _ownedCluster;
    private readonly Random _suffix = new();

    public CouchbaseLogSink(
        string connectionString,
        string username,
        string password,
        string bucketName,
        string scopeName = "_default",
        string collectionName = "_default")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);

        // Connect synchronously here so the construction failure surfaces
        // at sink-build time rather than per-event. The SDK is async-first
        // so we wrap with GetAwaiter().GetResult — acceptable on a
        // one-shot startup path.
        var cluster = Cluster.ConnectAsync(connectionString, username, password)
            .GetAwaiter().GetResult();
        var bucket = cluster.BucketAsync(bucketName).GetAwaiter().GetResult();
        var scope = bucket.ScopeAsync(scopeName).GetAwaiter().GetResult();
        _collection = scope.CollectionAsync(collectionName).GetAwaiter().GetResult();
        _ownedCluster = cluster;
    }

    /// <summary>
    /// Code-first overload accepts a pre-built <see cref="ICouchbaseCollection"/>
    /// — typical when the app already shares a cluster with its
    /// repositories.
    /// </summary>
    public CouchbaseLogSink(ICouchbaseCollection collection)
    {
        ArgumentNullException.ThrowIfNull(collection);
        _collection = collection;
        _ownedCluster = null;
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        var (key, document) = BuildDocument(logEvent);
        _collection.UpsertAsync(key, document).GetAwaiter().GetResult();
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        // Couchbase has no native bulk upsert in the public API; fan out
        // upserts and wait. The SDK's connection pool handles concurrency.
        var tasks = new List<System.Threading.Tasks.Task>(events.Count);
        foreach (var evt in events)
        {
            var (key, document) = BuildDocument(evt);
            tasks.Add(_collection.UpsertAsync(key, document));
        }
        System.Threading.Tasks.Task.WhenAll(tasks).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _ownedCluster?.Dispose();
    }

    private (string Key, Dictionary<string, object?> Document) BuildDocument(LogEvent evt)
    {
        var key = $"{evt.TimeUtc.UtcDateTime:yyyyMMdd}#{long.MaxValue - evt.TimeUtc.UtcTicks:D19}#{_suffix.Next():X8}";

        var document = new Dictionary<string, object?>
        {
            ["TimeUtc"] = evt.TimeUtc.UtcDateTime.ToString("O"),
            ["Level"] = evt.Level.Key,
            ["Category"] = evt.Category.Value,
            ["Message"] = evt.Message ?? string.Empty,
            ["Template"] = evt.MessageTemplate ?? string.Empty,
        };

        if (evt.Properties is not null && evt.Properties.Count > 0)
        {
            var properties = new Dictionary<string, object?>(evt.Properties.Count);
            foreach (var prop in evt.Properties)
            {
                properties[prop.Name] = prop.ResolvedValue;
            }
            document["Properties"] = properties;
        }

        return (key, document);
    }
}
