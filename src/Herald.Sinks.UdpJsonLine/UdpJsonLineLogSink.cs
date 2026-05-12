// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MMP.Herald;
using MMP.Herald.Events;
using MMP.Herald.Formatting;
using MMP.Herald.Levels;
using MMP.Herald.Pipeline;

namespace Herald.Sinks.UdpJsonLine;

/// <summary>
/// UDP sink that sends newline-delimited JSON datagrams to a remote endpoint.
/// Fire-and-forget: one datagram per event, no acknowledgement, no retry.
/// Suitable for syslog-style ingestion points and lossy-tolerant pipelines
/// (metrics-adjacent logging, local statsd-style relays).
///
/// <para>
/// <b>Operational notes.</b> UDP has no delivery guarantee — events are
/// dropped silently by the network stack if the buffer is full, the
/// destination is down, or a router drops the packet. Pair with a more
/// durable sink (file or TCP) if every event must survive. The sink does
/// not attempt reassembly: one event must fit in one datagram; oversized
/// events throw so the caller sees the misconfiguration. Most MTUs limit
/// effective payload to ~1,472 bytes; this sink enforces 65,000 bytes as
/// the hard ceiling (theoretical max datagram less UDP/IP headers).
/// </para>
///
/// <para>
/// <b>TLS posture.</b> There is no encrypted UDP primitive in the BCL.
/// Like the TCP sink, this writes plaintext bytes. Do not point it at a
/// public endpoint without a DTLS-terminating sidecar or VPN.
/// </para>
/// </summary>
public sealed class UdpJsonLineLogSink : ILogger, IBatchedLogSink, IDisposable
{
    // ~65 KB - upper bound on a single UDP datagram payload. Actual MTU on
    // the path is usually lower (1,472 bytes for Ethernet); sending larger
    // payloads risks fragmentation. We reject at the ceiling so a caller
    // with a runaway property bag sees the error at the source.
    private const int MaxDatagramBytes = 65_000;

    private readonly string _host;
    private readonly int _port;
    private readonly JsonFormatter _formatter;
    private readonly Lazy<IPEndPoint> _endpoint;
    private readonly UdpClient _client;
    private bool _disposed;

    public UdpJsonLineLogSink(
        string host,
        int port,
        ILogLevelRegistry levelRegistry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentNullException.ThrowIfNull(levelRegistry);

        if (port <= 0 || port > 65535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(port),
                port,
                "UDP sink port must be between 1 and 65535.");
        }

        _host = host;
        _port = port;
        _formatter = new JsonFormatter(levelRegistry);
        _client = new UdpClient(AddressFamily.InterNetwork);
        // Endpoint resolution is deferred — hostnames that resolve slowly or
        // point at a currently-unreachable host should not block construction.
        _endpoint = new Lazy<IPEndPoint>(ResolveEndpoint, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        LogBatch([logEvent]);
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;
        ObjectDisposedException.ThrowIf(_disposed, this);

        var target = _endpoint.Value;
        foreach (var logEvent in events)
        {
            var bytes = EncodeDatagram(logEvent);
            _client.Send(bytes, bytes.Length, target);
        }
    }

    public async ValueTask LogAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        await LogBatchAsync([logEvent], cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask LogBatchAsync(IReadOnlyList<LogEvent> events, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;
        ObjectDisposedException.ThrowIf(_disposed, this);

        var target = _endpoint.Value;
        foreach (var logEvent in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = EncodeDatagram(logEvent);
            await _client.SendAsync(bytes.AsMemory(0, bytes.Length), target, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Host the sink is configured to send to.</summary>
    public string Host => _host;

    /// <summary>Port the sink is configured to send to.</summary>
    public int Port => _port;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _client.Dispose(); } catch { /* best effort */ }
    }

    private IPEndPoint ResolveEndpoint()
    {
        // Resolving once at first-send; the address is cached for the lifetime
        // of the sink. Callers that need DNS-TTL-aware behavior should recreate
        // the sink or wrap it with a TTL-aware provider.
        var addresses = Dns.GetHostAddresses(_host);
        if (addresses.Length == 0)
        {
            throw new InvalidOperationException(
                $"UDP sink could not resolve host '{_host}'.");
        }
        return new IPEndPoint(addresses[0], _port);
    }

    private byte[] EncodeDatagram(LogEvent logEvent)
    {
        var line = _formatter.Format(logEvent);
        var byteCount = Encoding.UTF8.GetByteCount(line);
        if (byteCount > MaxDatagramBytes)
        {
            throw new InvalidOperationException(
                $"UDP sink refusing to send a {byteCount}-byte datagram; per-event payload must be under {MaxDatagramBytes} bytes. Trim properties or switch to the TCP sink for large events.");
        }
        return Encoding.UTF8.GetBytes(line);
    }
}
