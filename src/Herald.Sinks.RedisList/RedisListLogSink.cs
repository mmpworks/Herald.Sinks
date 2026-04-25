// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MMP.Herald;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using StackExchange.Redis;

namespace Herald.Sinks.RedisList;

/// <summary>
/// Sink that RPUSHes each log event as a JSON entry onto a Redis list.
/// Pairs with downstream consumers using <c>BLPOP</c> / <c>BRPOPLPUSH</c>
/// for durable handoff to a worker tier. Drop-in equivalent for
/// Serilog.Sinks.Redis.List.
/// </summary>
/// <remarks>
/// <para>
/// <b>Delivery semantics.</b> Unlike PubSub, list entries persist until
/// a consumer pops them — outages on the consumer side queue up rather
/// than silently dropping. Pair with <see cref="MaxLength"/> to cap
/// memory growth when consumers are slow.
/// </para>
/// <para>
/// <b>Capped lists.</b> When <see cref="MaxLength"/> is positive each
/// push runs an LTRIM to keep the list at no more than that many
/// entries (oldest evicted first). Set to zero or negative to disable.
/// </para>
/// <para>
/// <b>Thread safety.</b> StackExchange.Redis is thread-safe per the
/// driver contract; concurrent <c>Log</c> calls share the multiplexer's
/// connection pool.
/// </para>
/// </remarks>
public sealed class RedisListLogSink : ILogger, IBatchedLogSink, IDisposable
{
    private readonly IDatabase _database;
    private readonly RedisKey _listKey;
    private readonly long _maxLength;
    private readonly ConnectionMultiplexer? _ownedMultiplexer;

    public int MaxLength => (int)_maxLength;

    public RedisListLogSink(string connectionString, string listKey = "herald-logs", int maxLength = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(listKey);

        _ownedMultiplexer = ConnectionMultiplexer.Connect(connectionString);
        _database = _ownedMultiplexer.GetDatabase();
        _listKey = listKey;
        _maxLength = maxLength;
    }

    /// <summary>
    /// Code-first overload for callers that already own a connected
    /// <see cref="IDatabase"/>. The sink does not dispose the database
    /// reference because it does not own the multiplexer.
    /// </summary>
    public RedisListLogSink(IDatabase database, string listKey = "herald-logs", int maxLength = 0)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(listKey);

        _database = database;
        _listKey = listKey;
        _maxLength = maxLength;
        _ownedMultiplexer = null;
    }

    public void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        var payload = SerializeEvent(logEvent);
        _database.ListRightPush(_listKey, payload);
        TrimIfBounded();
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        var values = new RedisValue[events.Count];
        for (int i = 0; i < events.Count; i++)
        {
            values[i] = SerializeEvent(events[i]);
        }
        _database.ListRightPush(_listKey, values);
        TrimIfBounded();
    }

    public void Dispose()
    {
        _ownedMultiplexer?.Dispose();
    }

    private void TrimIfBounded()
    {
        if (_maxLength <= 0) return;
        // Keep the last _maxLength entries; -1 means "to the end of the list".
        // LTRIM is O(N) where N is the number of removed entries, but on a
        // capped list N is bounded by the burst size between trims.
        _database.ListTrim(_listKey, -_maxLength, -1);
    }

    private static string SerializeEvent(LogEvent evt)
    {
        // Utf8JsonWriter keeps the path AOT-clean and avoids per-event
        // allocation of a JsonSerializerOptions instance.
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("time_utc", evt.TimeUtc);
            writer.WriteString("level", evt.Level.Key);
            writer.WriteString("category", evt.Category.Value);
            writer.WriteString("message", evt.Message ?? string.Empty);
            writer.WriteString("template", evt.MessageTemplate ?? string.Empty);

            if (evt.Properties is not null && evt.Properties.Count > 0)
            {
                writer.WriteStartObject("properties");
                foreach (var prop in evt.Properties)
                {
                    WriteJsonValue(writer, prop.Name, prop.ResolvedValue);
                }
                writer.WriteEndObject();
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
