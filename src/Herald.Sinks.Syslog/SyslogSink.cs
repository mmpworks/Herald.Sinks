// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Events;

namespace Herald.Sinks.Syslog;

/// <summary>
/// Sink that emits Herald log events as syslog messages over UDP or
/// TCP. Supports RFC 5424 (modern, default) and RFC 3164 (BSD-legacy)
/// framing. Drop-in equivalent of Serilog.Sinks.SyslogMessages.
/// </summary>
/// <remarks>
/// <para>
/// <b>Transports.</b> UDP is the canonical syslog wire — lossy but
/// cheap, port 514 by default. TCP adds reliability and is required
/// for payloads that exceed an IP datagram; TCP framing uses octet
/// counting per RFC 6587 (<c>{length} {message}</c>).
/// </para>
/// <para>
/// <b>Priority.</b> <c>facility * 8 + severity</c>. Facility is fixed
/// at sink construction (typically <see cref="SyslogFacility.User"/>
/// or one of the Local0-Local7 slots). Severity maps from Herald's
/// level: Debug/Trace → 7, Info → 6, Notice/Success → 5, Warn → 4,
/// Error → 3, Critical → 2, Security → 1.
/// </para>
/// <para>
/// <b>Thread safety.</b> UDP writes are stateless on the client side —
/// safe under concurrent calls. TCP writes hold a per-sink lock so two
/// concurrent events do not interleave frames on the same connection.
/// </para>
/// </remarks>
public sealed class SyslogSink : HeraldSinkBase, IDisposable, INetworkSink
{
    private readonly string _host;
    private readonly int _port;
    private readonly SyslogFormat _format;
    private readonly SyslogTransport _transport;
    private readonly SyslogFacility _facility;
    private readonly string _logSourceHost;
    private readonly string _appName;
    private readonly string? _processId;
    private readonly string _structuredDataId;
    private readonly bool _structuredDataEnabled;

    // UDP state — a single socket per sink, bound lazily.
    private UdpClient? _udp;

    // TCP state — stream kept open across writes, rebuilt on failure.
    // The sink owns the TCP client and disposes it in Dispose.
    private TcpClient? _tcp;
    private NetworkStream? _tcpStream;
    private readonly object _tcpSync = new();

    /// <summary>
    /// Create a Syslog sink.
    /// </summary>
    /// <remarks>
    /// Two new RFC 5424 knobs land in this ctor (RFC 3164 ignores them
    /// — that format has no structured-data block):
    /// <list type="bullet">
    ///   <item><paramref name="structuredDataId"/> is the SD-ID written
    ///         on the SD-ELEMENT that carries Herald properties.
    ///         Defaults to <c>herald@32473</c> — the IANA example
    ///         enterprise number works against any collector without
    ///         registration. Operators with a real IANA-assigned
    ///         enterprise number should pin theirs here.</item>
    ///   <item><paramref name="structuredDataEnabled"/> toggles SD
    ///         emission. Defaults to true — events with no properties
    ///         still emit NILVALUE ("-"). Operators on collectors that
    ///         reject SD frames can flip this off to fall back to the
    ///         prior "drop everything" behaviour, though that loses
    ///         structured data on the wire.</item>
    /// </list>
    /// </remarks>
    public SyslogSink(
        string host,
        int port = 514,
        SyslogFormat format = SyslogFormat.Rfc5424,
        SyslogTransport transport = SyslogTransport.Udp,
        SyslogFacility facility = SyslogFacility.User,
        string? logSourceHost = null,
        string? appName = null,
        string? processId = null,
        string? structuredDataId = null,
        bool structuredDataEnabled = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        if (port is <= 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be 1..65535.");

        _host = host;
        _port = port;
        _format = format;
        _transport = transport;
        _facility = facility;
        _logSourceHost = logSourceHost ?? Environment.MachineName;
        _appName = appName ?? "herald";
        _processId = processId ?? Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _structuredDataId = string.IsNullOrWhiteSpace(structuredDataId) ? "herald@32473" : structuredDataId;
        _structuredDataEnabled = structuredDataEnabled;
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        var message = SyslogMessageBuilder.Build(
            logEvent, _format, _facility, _logSourceHost, _appName, _processId,
            _structuredDataId, _structuredDataEnabled);

        var bytes = Encoding.UTF8.GetBytes(message);

        if (_transport == SyslogTransport.Udp)
            SendUdp(bytes);
        else
            SendTcp(bytes);
    }

    private void SendUdp(byte[] payload)
    {
        // Lazily construct the UdpClient; the initial constructor cost
        // is ~50 us on first call, then sub-microsecond thereafter.
        var client = _udp;
        if (client is null)
        {
            var created = new UdpClient();
            client = Interlocked.CompareExchange(ref _udp, created, null) ?? created;
            if (!ReferenceEquals(client, created)) created.Dispose();
        }

        client.Send(payload, payload.Length, _host, _port);
    }

    private void SendTcp(byte[] payload)
    {
        // RFC 6587 octet-counting framing: "{length} {message}".
        // Frame the message before locking so the lock window is just
        // the write + the reconnect-on-failure.
        var lengthPrefix = Encoding.ASCII.GetBytes(
            payload.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ");

        lock (_tcpSync)
        {
            try
            {
                EnsureTcpConnected();
                _tcpStream!.Write(lengthPrefix, 0, lengthPrefix.Length);
                _tcpStream.Write(payload, 0, payload.Length);
                _tcpStream.Flush();
            }
            catch (IOException)
            {
                // Transient TCP error — drop the connection and retry
                // once. Typical cause: collector restarted. On a second
                // failure the exception propagates.
                ResetTcp();
                EnsureTcpConnected();
                _tcpStream!.Write(lengthPrefix, 0, lengthPrefix.Length);
                _tcpStream.Write(payload, 0, payload.Length);
                _tcpStream.Flush();
            }
            catch (SocketException)
            {
                ResetTcp();
                EnsureTcpConnected();
                _tcpStream!.Write(lengthPrefix, 0, lengthPrefix.Length);
                _tcpStream.Write(payload, 0, payload.Length);
                _tcpStream.Flush();
            }
        }
    }

    private void EnsureTcpConnected()
    {
        if (_tcp is { Connected: true } && _tcpStream is not null) return;

        var client = new TcpClient { NoDelay = true };
        client.Connect(_host, _port);
        _tcp = client;
        _tcpStream = client.GetStream();
    }

    private void ResetTcp()
    {
        _tcpStream?.Dispose();
        _tcpStream = null;
        _tcp?.Dispose();
        _tcp = null;
    }

    public void Dispose()
    {
        _udp?.Dispose();
        _udp = null;
        ResetTcp();
    }
}
