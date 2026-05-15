// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Pipeline;
using NetMQ;
using NetMQ.Sockets;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.ZeroMQ;

/// <summary>
/// ZeroMQ socket kind. PubSub fans events out to every subscriber;
/// PushPull load-balances events across pullers like a work queue.
/// </summary>
public enum ZeroMqSocketKind
{
    /// <summary>PUB socket — fan-out to every subscriber. Default.</summary>
    PubSub,
    /// <summary>PUSH socket — load-balance across connected pullers.</summary>
    PushPull
}

/// <summary>
/// Sink that publishes log events as JSON frames over a ZeroMQ socket
/// via NetMQ. Drop-in for Serilog.Sinks.ZeroMQ. Supports both PUB
/// (fan-out to subscribers) and PUSH (load-balance to pullers) socket
/// kinds.
/// </summary>
/// <remarks>
/// <para>
/// <b>Threading model.</b> NetMQ sockets are not thread-safe and must
/// be accessed from a single thread. The sink owns a dedicated worker
/// task that creates the socket and runs a <see cref="NetMQPoller"/>;
/// producers Log() from any thread by enqueuing onto a thread-safe
/// <see cref="NetMQQueue{T}"/>. The poller drains the queue and sends
/// each message via the socket.
/// </para>
/// <para>
/// <b>Bind vs connect.</b> Defaults match canonical patterns:
/// PUB binds (acts as a server, subscribers connect to it), PUSH
/// connects (the puller binds, workers connect). Override <c>bind</c>
/// to flip the relationship.
/// </para>
/// <para>
/// <b>Topics.</b> For PUB, the sink prepends a topic frame so
/// subscribers can filter via SUBSCRIBE. PUSH ignores the topic
/// (PUSH/PULL has no topic concept).
/// </para>
/// <para>
/// <b>Delivery semantics.</b> ZeroMQ is best-effort. PUB drops messages
/// for slow subscribers; PUSH blocks the queue if no pullers are
/// connected. Pair with a buffered upstream (Herald's WithAsyncLogging)
/// for durability under load.
/// </para>
/// </remarks>
public sealed class ZeroMqLogSink : HeraldSinkBase, IDisposable
{
    private readonly NetMQQueue<string> _queue;
    private readonly NetMQPoller _poller;
    private readonly Task _pollerTask;
    private readonly string _topic;
    private readonly ZeroMqSocketKind _kind;
    private readonly TaskCompletionSource _socketReady = new();

    public ZeroMqLogSink(
        string endpoint,
        string topic = "herald-logs",
        ZeroMqSocketKind kind = ZeroMqSocketKind.PubSub,
        bool? bind = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        _topic = topic;
        _kind = kind;
        var shouldBind = bind ?? (kind == ZeroMqSocketKind.PubSub);

        _queue = new NetMQQueue<string>();
        _poller = new NetMQPoller { _queue };

        // Run the poller on a dedicated long-running task so the socket
        // and poller live entirely on one thread, matching NetMQ's
        // single-threaded contract.
        _pollerTask = Task.Factory.StartNew(
            () => RunPoller(endpoint, shouldBind),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        // Wait for the socket to come online on first use; subsequent
        // calls hit the already-completed task and are effectively free.
        _socketReady.Task.GetAwaiter().GetResult();
        _queue.Enqueue(SerializeEvent(logEvent));
    }

    public void Dispose()
    {
        _queue.Enqueue(string.Empty); // wake the poller for a final drain
        _poller.StopAsync();
        try { _pollerTask.Wait(TimeSpan.FromSeconds(2)); }
        catch (AggregateException) { }
        _poller.Dispose();
        _queue.Dispose();
    }

    private void RunPoller(string endpoint, bool shouldBind)
    {
        // The socket is created on this thread and never escapes — that
        // satisfies NetMQ's single-thread-per-socket contract. The
        // ReceiveReady handler also runs on this thread.
        using NetMQSocket socket = _kind switch
        {
            ZeroMqSocketKind.PubSub => new PublisherSocket(),
            ZeroMqSocketKind.PushPull => new PushSocket(),
            _ => throw new ArgumentOutOfRangeException(nameof(_kind))
        };

        if (shouldBind)
        {
            socket.Bind(endpoint);
        }
        else
        {
            socket.Connect(endpoint);
        }

        _queue.ReceiveReady += (_, args) =>
        {
            while (args.Queue.TryDequeue(out var message, TimeSpan.Zero))
            {
                if (string.IsNullOrEmpty(message)) continue; // shutdown sentinel
                if (_kind == ZeroMqSocketKind.PubSub)
                {
                    socket.SendMoreFrame(_topic).SendFrame(message);
                }
                else
                {
                    socket.SendFrame(message);
                }
            }
        };

        _socketReady.TrySetResult();
        _poller.Run();
    }

    private static string SerializeEvent(LogEvent evt)
    {
        // Utf8JsonWriter keeps the path AOT-clean.
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
