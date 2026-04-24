// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using MMP.Herald;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Pipeline;

namespace Herald.Sinks.Otlp;

/// <summary>
/// Sink that posts log events as OTLP protobuf to an OpenTelemetry collector endpoint.
/// Uses application/x-protobuf content type for binary protobuf wire format.
/// Posts to {endpoint}/v1/logs.
/// The HttpClient is pooled internally (SocketsHttpHandler manages connection reuse).
/// If the caller provides an HttpClient, the sink does not own it and will not dispose it.
/// </summary>
public sealed class OtlpProtobufLogSink : ILogger, IBatchedLogSink, IDisposable
{
    private readonly Uri _endpoint;
    private readonly OtlpProtobufLogSerializer _serializer;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public OtlpProtobufLogSink(
        string endpoint,
        ILogLevelRegistry levelRegistry,
        IReadOnlyDictionary<string, string>? resourceAttributes = null,
        HttpClient? httpClient = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentNullException.ThrowIfNull(levelRegistry);

        _endpoint = new Uri(endpoint.TrimEnd('/') + "/v1/logs", UriKind.Absolute);
        _serializer = new OtlpProtobufLogSerializer(levelRegistry, resourceAttributes);
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public void Log(LogEvent logEvent) {
        ArgumentNullException.ThrowIfNull(logEvent);
        LogBatch([logEvent]);
    }

    public void LogBatch(IReadOnlyList<LogEvent> events) {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        var payload = _serializer.SerializeBatch(events);

        using var content = new ByteArrayContent(payload);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-protobuf");

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = content };
        using var response = _httpClient.Send(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
