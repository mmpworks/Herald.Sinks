// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using NATS.Client.Core;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.Nats;

/// <summary>
/// Sink that publishes log events as JSON messages to a NATS subject
/// via NATS.Client.Core (the modern v2 line). Connects on construction
/// and shares the connection across all calls.
/// </summary>
public sealed class NatsLogSink : HeraldSinkBase, IAsyncDisposable, INetworkSink
{
    private readonly NatsConnection _connection;
    private readonly string _subject;
    private readonly bool _ownsConnection;

    public NatsLogSink(string url, string subject = "herald.logs")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        var opts = new NatsOpts { Url = url };
        _connection = new NatsConnection(opts);
        _subject = subject;
        _ownsConnection = true;
    }

    public NatsLogSink(NatsConnection connection, string subject = "herald.logs")
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        _connection = connection;
        _subject = subject;
        _ownsConnection = false;
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        var payload = SerializeEvent(logEvent);
        _connection.PublishAsync(_subject, payload).GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsConnection)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static string SerializeEvent(LogEvent evt)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("time_utc", evt.TimeUtc);
            writer.WriteString("level", evt.Level.Key);
            writer.WriteString("category", evt.Category.Value);
            writer.WriteString("message", evt.Message ?? string.Empty);
            writer.WriteString("template", evt.MessageTemplate ?? string.Empty);
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
