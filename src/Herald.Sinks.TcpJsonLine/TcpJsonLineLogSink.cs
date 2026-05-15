// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Events;
using MMP.Herald.Formatting;
using MMP.Herald.Levels;
using MMP.Herald.Pipeline;

namespace Herald.Sinks.TcpJsonLine;

/// <summary>
/// TCP sink that writes newline-delimited JSON lines to a remote socket.
/// Maintains a persistent connection and reconnects automatically on failure.
/// Thread-safe: both the sync and async paths share a <see cref="SemaphoreSlim"/>
/// so concurrent callers cannot interleave writes on the shared stream.
///
/// <para>
/// <b>TLS posture.</b> This sink speaks plaintext TCP. Events travel
/// unencrypted on the wire. Do not point it at a public endpoint, a
/// cross-VPC hop, or any network where a capture could catch PII or
/// secrets. For encrypted transport, terminate TLS at a sidecar
/// (stunnel, HAProxy, envoy) or tunnel the connection through a
/// service mesh / VPN. A first-party TLS variant is tracked as a
/// future Pro / Enterprise sink; until it ships, treat this sink as
/// a LAN / trusted-network primitive only.
/// </para>
/// </summary>
public sealed class TcpJsonLineLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable, INetworkSink
{
    private readonly string _host;
    private readonly int _port;
    private readonly JsonFormatter _formatter;
    // SemaphoreSlim so Log (Wait) and LogAsync (WaitAsync) share one lock
    // over the single TCP stream. A plain `lock` cannot span an await.
    private readonly SemaphoreSlim _gate = new(1, 1);

    private TcpClient? _client;
    private StreamWriter? _writer;
    private bool _disposed;

    public TcpJsonLineLogSink(
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
                "TCP sink port must be between 1 and 65535.");
        }

        _host = host;
        _port = port;
        _formatter = new JsonFormatter(levelRegistry);
    }

    public override void Log(LogEvent logEvent) {
        ArgumentNullException.ThrowIfNull(logEvent);
        LogBatch([logEvent]);
    }

    public void LogBatch(IReadOnlyList<LogEvent> events) {
        ArgumentNullException.ThrowIfNull(events);

        if (events.Count == 0)
        {
            return;
        }

        _gate.Wait();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            try
            {
                EnsureConnected();
                WriteEvents(events);
            }
            catch
            {
                // Connection failed or write failed - tear down and reconnect on next call.
                CloseConnection();
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask LogAsync(LogEvent logEvent, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(logEvent);
        await LogBatchAsync([logEvent], cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask LogBatchAsync(IReadOnlyList<LogEvent> events, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(events);

        if (events.Count == 0)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            try
            {
                EnsureConnected();
                await WriteEventsAsync(events, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                CloseConnection();
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Whether the sink currently holds an open TCP connection.
    /// </summary>
    public bool IsConnected
    {
        get
        {
            _gate.Wait();
            try
            {
                return _client?.Connected == true;
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    public void Dispose() {
        // Fast-path the second-call case so we never try to Wait on a
        // SemaphoreSlim that has already been disposed by the first call.
        if (_disposed)
        {
            return;
        }

        _gate.Wait();
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CloseConnection();
        }
        finally
        {
            _gate.Release();
        }
        _gate.Dispose();
    }

    private void EnsureConnected() {
        if (_client?.Connected == true)
        {
            return;
        }

        // Previous connection is dead or never established - clean up and reconnect.
        CloseConnection();

        _client = new TcpClient();
        _client.Connect(_host, _port);

        var stream = _client.GetStream();
        _writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 1024,
            leaveOpen: false)
        {
            AutoFlush = true
        };
    }

    private void WriteEvents(IReadOnlyList<LogEvent> events) {
        foreach (var logEvent in events)
        {
            _writer!.WriteLine(_formatter.Format(logEvent));
        }
    }

    private async ValueTask WriteEventsAsync(IReadOnlyList<LogEvent> events, CancellationToken cancellationToken) {
        foreach (var logEvent in events)
        {
            await _writer!.WriteLineAsync(_formatter.Format(logEvent).AsMemory(), cancellationToken).ConfigureAwait(false);
        }
    }

    private void CloseConnection() {
        try { _writer?.Dispose(); } catch { /* best effort */ }
        try { _client?.Dispose(); } catch { /* best effort */ }
        _writer = null;
        _client = null;
    }
}
