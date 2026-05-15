// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Events;
using MMP.Herald.Formatting;
using MMP.Herald.Levels;
using MMP.Herald.Pipeline;

namespace Herald.Sinks.HttpJson;

/// <summary>
/// HTTP sink that posts newline-delimited JSON payloads to a remote endpoint.
/// Single events are sent as one-line NDJSON payloads.
/// Batched events are sent as multi-line NDJSON payloads.
/// The HttpClient is pooled internally (SocketsHttpHandler manages connection reuse).
/// If the caller provides an HttpClient, the sink does not own it and will not dispose it.
/// </summary>
public sealed class HttpJsonLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable, INetworkSink
{
    private readonly Uri _endpoint;
    private readonly JsonFormatter _formatter;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public HttpJsonLogSink(
        string uri,
        ILogLevelRegistry levelRegistry,
        HttpClient? httpClient = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        ArgumentNullException.ThrowIfNull(levelRegistry);

        _endpoint = new Uri(uri, UriKind.Absolute);
        _formatter = new JsonFormatter(levelRegistry);
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
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

        using var request = BuildRequest(events);
        // The sink still offers a sync path for callers that need it — for the
        // hot path the AsyncLogger drains via LogAsync below, which uses
        // SendAsync so the worker thread is not pinned on socket I/O.
        using var response = _httpClient.Send(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();
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

        using var request = BuildRequest(events);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage BuildRequest(IReadOnlyList<LogEvent> events) =>
        new(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(BuildNdjsonPayload(events), Encoding.UTF8, "application/x-ndjson")
        };

    public void Dispose() {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private string BuildNdjsonPayload(IReadOnlyList<LogEvent> events) {
        var builder = new StringBuilder();

        for (var index = 0; index < events.Count; index += 1)
        {
            if (index > 0)
            {
                builder.Append('\n');
            }

            builder.Append(_formatter.Format(events[index]));
        }

        return builder.ToString();
    }
}
