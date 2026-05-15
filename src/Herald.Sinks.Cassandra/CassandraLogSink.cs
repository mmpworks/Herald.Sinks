// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Cassandra;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.Cassandra;

/// <summary>
/// Sink that INSERTs log events into a Cassandra table via the DataStax
/// CassandraCSharpDriver. Drop-in equivalent for Serilog.Sinks.Cassandra.
/// Compatible with ScyllaDB and other Cassandra-protocol clusters.
/// </summary>
/// <remarks>
/// <para>
/// <b>Schema.</b> The sink expects a table with at least these columns:
/// <c>partition_date</c> (text), <c>time_uuid</c> (timeuuid), <c>level</c>
/// (text), <c>category</c> (text), <c>message</c> (text), <c>template</c>
/// (text), <c>properties</c> (text). Partition by <c>partition_date</c>
/// (yyyymmdd) and cluster by <c>time_uuid DESC</c> for newest-first
/// scans. Schema creation is operator responsibility.
/// </para>
/// <para>
/// <b>Prepared statements.</b> The INSERT is prepared once on
/// construction and bound per-event so the per-call cost stays bounded.
/// </para>
/// <para>
/// <b>Thread safety.</b> The DataStax <see cref="ISession"/> is
/// thread-safe per the driver contract; concurrent Log calls share the
/// session's connection pool.
/// </para>
/// </remarks>
public sealed class CassandraLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable
{
    private readonly ISession _session;
    private readonly PreparedStatement _insert;
    private readonly Cluster? _ownedCluster;

    public CassandraLogSink(string[] contactPoints, string keyspace, string tableName = "herald_logs")
    {
        ArgumentNullException.ThrowIfNull(contactPoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        if (contactPoints.Length == 0)
        {
            throw new ArgumentException("At least one contact point is required.", nameof(contactPoints));
        }

        _ownedCluster = Cluster.Builder().AddContactPoints(contactPoints).Build();
        _session = _ownedCluster.Connect(keyspace);
        _insert = PrepareInsert(_session, tableName);
    }

    /// <summary>
    /// Code-first overload accepts a pre-connected <see cref="ISession"/>.
    /// The sink does not dispose the session because it does not own it.
    /// </summary>
    public CassandraLogSink(ISession session, string tableName = "herald_logs")
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        _session = session;
        _insert = PrepareInsert(session, tableName);
        _ownedCluster = null;
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        _session.Execute(BindStatement(logEvent));
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        // Cassandra logged batches across partitions are an anti-pattern
        // (coordinator load); we issue per-event Execute calls and let
        // the session's connection pool handle concurrency. For
        // single-partition optimisations the operator can bind a custom
        // batch on top via the code-first overload.
        foreach (var evt in events)
        {
            _session.Execute(BindStatement(evt));
        }
    }

    public void Dispose()
    {
        _ownedCluster?.Dispose();
    }

    private static PreparedStatement PrepareInsert(ISession session, string tableName)
    {
        // Prepared on construction so per-event cost is just a Bind +
        // Execute round trip. The CQL is parameterised; tableName is
        // validated against a strict character class to prevent injection.
        if (!IsValidIdentifier(tableName))
        {
            throw new ArgumentException(
                $"Invalid table name '{tableName}'. Cassandra identifiers must match [a-zA-Z_][a-zA-Z0-9_]*.",
                nameof(tableName));
        }

        var cql = $"""
            INSERT INTO {tableName}
              (partition_date, time_uuid, level, category, message, template, properties)
            VALUES (?, ?, ?, ?, ?, ?, ?)
            """;
        return session.Prepare(cql);
    }

    private BoundStatement BindStatement(LogEvent evt)
    {
        var partitionDate = evt.TimeUtc.UtcDateTime.ToString("yyyyMMdd");
        var timeUuid = TimeUuid.NewId(evt.TimeUtc.UtcDateTime);
        var properties = evt.Properties is { Count: > 0 } ? SerializeProperties(evt) : null;

        return _insert.Bind(
            partitionDate,
            timeUuid,
            evt.Level.Key,
            evt.Category.Value,
            evt.Message ?? string.Empty,
            evt.MessageTemplate ?? string.Empty,
            properties);
    }

    private static bool IsValidIdentifier(string name) =>
        System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]*$");

    private static string SerializeProperties(LogEvent evt)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var prop in evt.Properties)
            {
                WriteJsonValue(writer, prop.Name, prop.ResolvedValue);
            }
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteJsonValue(Utf8JsonWriter writer, string name, object? value)
    {
        switch (value)
        {
            case null: writer.WriteNull(name); break;
            case string s: writer.WriteString(name, s); break;
            case bool b: writer.WriteBoolean(name, b); break;
            case int i: writer.WriteNumber(name, i); break;
            case long l: writer.WriteNumber(name, l); break;
            case double d: writer.WriteNumber(name, d); break;
            case float f: writer.WriteNumber(name, f); break;
            case decimal m: writer.WriteNumber(name, m); break;
            case DateTime dt: writer.WriteString(name, dt.ToUniversalTime()); break;
            case DateTimeOffset dto: writer.WriteString(name, dto); break;
            case Guid g: writer.WriteString(name, g); break;
            default: writer.WriteString(name, value.ToString() ?? string.Empty); break;
        }
    }
}
