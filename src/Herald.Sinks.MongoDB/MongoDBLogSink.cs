// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Herald.Sinks.MongoDB;

/// <summary>
/// Sink that writes log events as BSON documents to a MongoDB
/// collection. Single <see cref="IMongoCollection{TDocument}.InsertOne"/>
/// on <c>Log</c>, <see cref="IMongoCollection{TDocument}.InsertMany"/>
/// on batches via <see cref="IBatchedLogSink"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Document shape.</b> Each event becomes a flat BSON document:
/// <c>time_utc</c> (Date), <c>level</c> (String), <c>category</c>
/// (String), <c>message</c> (String), <c>template</c> (String),
/// <c>exception</c> (String, optional), <c>properties</c> (embedded
/// document). Property values serialize via BsonValue conversion —
/// primitives round-trip; complex objects fall back to their string
/// representation.
/// </para>
/// <para>
/// <b>Capped collections.</b> For bounded retention pair the sink
/// with a capped collection on the MongoDB side. The sink does not
/// manage capping — that's a DDL concern owned by the installer.
/// </para>
/// <para>
/// <b>Thread safety.</b> The sink is thread-safe. The MongoClient
/// instance it wraps is thread-safe per the MongoDB driver's
/// contract; concurrent Log calls share the single connection pool.
/// </para>
/// </remarks>
public sealed class MongoDBLogSink : HeraldSinkBase, IBatchedLogSink
{
    private readonly IMongoCollection<BsonDocument> _collection;

    public MongoDBLogSink(string connectionString, string databaseName, string collectionName = "logs")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);

        var client = new MongoClient(connectionString);
        var database = client.GetDatabase(databaseName);
        _collection = database.GetCollection<BsonDocument>(collectionName);
    }

    /// <summary>
    /// Code-first overload for callers that already own an
    /// <see cref="IMongoCollection{BsonDocument}"/> — typical when
    /// the app shares a <c>MongoClient</c> singleton with its
    /// repositories.
    /// </summary>
    public MongoDBLogSink(IMongoCollection<BsonDocument> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);
        _collection = collection;
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        _collection.InsertOne(BuildDocument(logEvent));
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        var documents = new List<BsonDocument>(events.Count);
        foreach (var evt in events)
            documents.Add(BuildDocument(evt));

        // Ordered=false so one failed document (e.g. oversize) does
        // not block the rest of the batch. The driver reports
        // per-document failures in the BulkWriteException.
        _collection.InsertMany(documents, new InsertManyOptions { IsOrdered = false });
    }

    private static BsonDocument BuildDocument(LogEvent evt)
    {
        var doc = new BsonDocument
        {
            { "time_utc", evt.TimeUtc.UtcDateTime },
            { "level", evt.Level.Key },
            { "category", evt.Category.Value },
            { "message", evt.Message ?? string.Empty },
            { "template", evt.MessageTemplate ?? string.Empty },
        };

        if (evt.Context.TryGetValue(LogContextKeys.Exception, out var exValue) && exValue is Exception ex)
        {
            doc.Add("exception", ex.ToString());
        }

        if (evt.Properties is not null && evt.Properties.Count > 0)
        {
            var props = new BsonDocument();
            foreach (var prop in evt.Properties)
            {
                props.Add(prop.Name, ConvertToBson(prop.ResolvedValue));
            }
            doc.Add("properties", props);
        }

        return doc;
    }

    private static BsonValue ConvertToBson(object? value)
    {
        // Map the common BCL primitive cases so property values
        // round-trip usefully. Anything the driver's BsonValue.Create
        // can't handle falls back to ToString() — better than an
        // insert-time exception from a custom type.
        return value switch
        {
            null => BsonNull.Value,
            string s => new BsonString(s),
            bool b => new BsonBoolean(b),
            int i => new BsonInt32(i),
            long l => new BsonInt64(l),
            double d => new BsonDouble(d),
            float f => new BsonDouble(f),
            decimal m => new BsonDecimal128(m),
            DateTime dt => new BsonDateTime(dt.ToUniversalTime()),
            DateTimeOffset dto => new BsonDateTime(dto.UtcDateTime),
            Guid g => new BsonString(g.ToString("D")),
            _ => new BsonString(value.ToString() ?? string.Empty),
        };
    }
}
