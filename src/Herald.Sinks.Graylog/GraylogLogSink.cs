// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Sinks.Batching;
using MMP.Herald.Events;

namespace Herald.Sinks.Graylog;

/// <summary>
/// Sink that emits Herald log events as GELF 1.1 messages to Graylog
/// over HTTP or TCP. Drop-in for Serilog.Sinks.Graylog /
/// Serilog.Sinks.Graylog.Batching.
/// </summary>
/// <remarks>
/// <para>
/// <b>Transports.</b> HTTP POSTs the GELF JSON to the Graylog input.
/// TCP sends null-terminated GELF frames on a persistent stream.
/// Both satisfy the GELF 1.1 spec. UDP with chunking is not in 1.0 —
/// HTTP covers the common case and TCP covers persistent / large-
/// payload deployments.
/// </para>
/// <para>
/// <b>Host field.</b> Defaults to <see cref="Environment.MachineName"/>.
/// Override via the ctor for container workloads where the machine
/// name is ephemeral.
/// </para>
/// </remarks>
public sealed class GraylogLogSink : BatchingNetworkSinkBase, IDisposable, INetworkSink
{
    private readonly GraylogTransport _transport;
    private readonly string _sourceHost;

    // HTTP state.
    private readonly Uri? _httpEndpoint;
    private readonly HttpClient? _httpClient;
    private readonly bool _ownsHttpClient;

    // TCP state.
    private readonly string? _tcpHost;
    private readonly int _tcpPort;
    private TcpClient? _tcpClient;
    private NetworkStream? _tcpStream;
    private readonly object _tcpSync = new();

    public GraylogLogSink(
        string endpoint,
        GraylogTransport transport = GraylogTransport.Http,
        string? sourceHost = null,
        HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        _transport = transport;
        _sourceHost = sourceHost ?? Environment.MachineName;

        if (transport == GraylogTransport.Http)
        {
            _httpEndpoint = new Uri(endpoint);
            _ownsHttpClient = httpClient is null;
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }
        else
        {
            (_tcpHost, _tcpPort) = ParseTcpEndpoint(endpoint);
        }
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        var gelf = GraylogMessageBuilder.Build(logEvent, _sourceHost);

        if (_transport == GraylogTransport.Http)
            SendHttp(gelf);
        else
            SendTcp(gelf);
    }

    private void SendHttp(string gelf)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _httpEndpoint)
        {
            Content = new StringContent(gelf, Encoding.UTF8, "application/json"),
        };
        using var response = _httpClient!.Send(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();
    }

    private void SendTcp(string gelf)
    {
        // GELF TCP framing is null-terminated UTF-8.
        var bytes = Encoding.UTF8.GetBytes(gelf + "\0");

        lock (_tcpSync)
        {
            try
            {
                EnsureTcpConnected();
                _tcpStream!.Write(bytes, 0, bytes.Length);
                _tcpStream.Flush();
            }
            catch (IOException)
            {
                ResetTcp();
                EnsureTcpConnected();
                _tcpStream!.Write(bytes, 0, bytes.Length);
                _tcpStream.Flush();
            }
            catch (SocketException)
            {
                ResetTcp();
                EnsureTcpConnected();
                _tcpStream!.Write(bytes, 0, bytes.Length);
                _tcpStream.Flush();
            }
        }
    }

    private void EnsureTcpConnected()
    {
        if (_tcpClient is { Connected: true } && _tcpStream is not null) return;

        var client = new TcpClient { NoDelay = true };
        client.Connect(_tcpHost!, _tcpPort);
        _tcpClient = client;
        _tcpStream = client.GetStream();
    }

    private void ResetTcp()
    {
        _tcpStream?.Dispose();
        _tcpStream = null;
        _tcpClient?.Dispose();
        _tcpClient = null;
    }

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient?.Dispose();
        ResetTcp();
    }

    private static (string Host, int Port) ParseTcpEndpoint(string endpoint)
    {
        // Accept 'host:port' form. 12201 is the Graylog TCP default.
        var colon = endpoint.LastIndexOf(':');
        if (colon < 0) return (endpoint, 12201);

        var host = endpoint[..colon];
        if (!int.TryParse(endpoint[(colon + 1)..], out var port))
        {
            port = 12201;
        }
        return (host, port);
    }
}
