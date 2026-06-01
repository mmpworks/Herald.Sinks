// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Herald.Sinks.Loki;
using MMP.Herald.Events;
using MMP.Herald.Sinks.Batching;
using Xunit;

namespace Herald.Sinks.Batching.Tests;

/// <summary>
/// End-to-end delivery regression. A mock HttpMessageHandler can pass while
/// the sink is logged-but-never-POSTed; only a real listener proves the
/// event left the process. This wires a real <see cref="LokiLogSink"/>
/// (pointed at a live <see cref="HttpListener"/>) behind the batching
/// decorator, logs 500 events, disposes, and asserts every event arrived —
/// in batches, not one POST per event.
/// </summary>
public sealed class BatchingLiveListenerRegressionTests
{
    [Fact]
    public async Task All_logged_events_reach_a_live_HTTP_listener_in_batches()
    {
        const int eventCount = 500;

        using var listener = new LiveLokiListener();
        listener.Start();

        // Real sink, real HttpClient, real socket to the listener.
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var sink = new LokiLogSink(endpoint: listener.BaseUrl, httpClient: httpClient);

        // Batch size 50 so 500 events land in ~10 POSTs, not 500.
        var options = new BatchingOptions(
            Enabled: true, BatchSize: 50, FlushIntervalMs: 200, QueueCapacity: 4096);
        var decorator = (BatchingLogSinkDecorator)BatchingLogSinkDecorator.Wrap(sink, options);

        for (var i = 0; i < eventCount; i++)
        {
            decorator.Log(TestEvents.Make($"event-{i}"));
        }

        // Dispose drains the backlog into the inner sink, which POSTs it.
        await decorator.DisposeAsync();

        // Give the listener a moment to finish reading any in-flight POSTs.
        await listener.WaitForLineCountAsync(eventCount, TimeSpan.FromSeconds(10));

        listener.TotalLogLines.Should().Be(eventCount,
            "every logged event must be delivered, none silently dropped");

        // Batched delivery: far fewer POSTs than events. With batch size 50,
        // 500 events should produce on the order of 10 requests, never 500.
        listener.RequestCount.Should().BeLessThan(eventCount,
            "delivery must be batched, not one POST per event");
        listener.RequestCount.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Minimal in-process HTTP listener that counts Loki push requests and
    /// the total number of log lines across all received streams.
    /// </summary>
    private sealed class LiveLokiListener : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _cts = new();
        private int _requestCount;
        private int _totalLogLines;
        private Task? _loop;

        public string BaseUrl { get; }

        public LiveLokiListener()
        {
            // Port 0 lets the OS pick a free port; read it back from Prefixes
            // is not supported, so bind an explicit ephemeral port instead.
            var port = GetFreePort();
            BaseUrl = $"http://127.0.0.1:{port}/";
            _listener.Prefixes.Add(BaseUrl);
        }

        public int RequestCount => Volatile.Read(ref _requestCount);
        public int TotalLogLines => Volatile.Read(ref _totalLogLines);

        public void Start()
        {
            _listener.Start();
            _loop = Task.Run(AcceptLoopAsync);
        }

        private async Task AcceptLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception) when (_cts.IsCancellationRequested)
                {
                    return;
                }
                catch (HttpListenerException)
                {
                    return;
                }

                try
                {
                    await HandleAsync(context).ConfigureAwait(false);
                }
                catch
                {
                    // Best-effort: a malformed body should not stop the loop.
                }
            }
        }

        private async Task HandleAsync(HttpListenerContext context)
        {
            Interlocked.Increment(ref _requestCount);

            string body;
            using (var reader = new System.IO.StreamReader(
                context.Request.InputStream, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            Interlocked.Add(ref _totalLogLines, CountLogLines(body));

            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            context.Response.Close();
        }

        // Count the "values" entries across every Loki stream in the push
        // payload — that is the number of log lines the sink delivered.
        private static int CountLogLines(string body)
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("streams", out var streams))
            {
                return 0;
            }

            var count = 0;
            foreach (var stream in streams.EnumerateArray())
            {
                if (stream.TryGetProperty("values", out var values))
                {
                    count += values.GetArrayLength();
                }
            }
            return count;
        }

        public async Task WaitForLineCountAsync(int count, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (TotalLogLines < count && DateTime.UtcNow < deadline)
            {
                await Task.Delay(25).ConfigureAwait(false);
            }
        }

        private static int GetFreePort()
        {
            var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _listener.Stop(); } catch { /* already stopped */ }
            try { _listener.Close(); } catch { /* already closed */ }
            try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch { /* shutdown */ }
            _cts.Dispose();
        }
    }
}
