// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Buffers;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Pipeline;

namespace Herald.Sinks.Elasticsearch;

/// <summary>
/// Sends log events to Elasticsearch as JSON documents via the Bulk API.
/// Uses index naming convention: {indexPrefix}-{yyyy.MM.dd} for time-based indices.
///
/// Supports both single-event and batch modes.
/// </summary>
public sealed class ElasticsearchLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable
{
    internal static readonly HeraldEdition MinEdition = HeraldEdition.Community;

    private readonly string _baseUrl;
    private readonly string _indexPrefix;
    private readonly ILogLevelRegistry _levelRegistry;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public ElasticsearchLogSink(
        string baseUrl,
        ILogLevelRegistry levelRegistry,
        string indexPrefix = "herald-logs",
        HttpClient? httpClient = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        if (!System.Text.RegularExpressions.Regex.IsMatch(indexPrefix, @"^[a-z0-9][a-z0-9_\-\.]{0,127}$"))
            throw new ArgumentException(
                "Index prefix must be 1-128 lowercase alphanumeric characters, hyphens, underscores, or dots.", nameof(indexPrefix));
        _baseUrl = baseUrl.TrimEnd('/');
        _levelRegistry = levelRegistry ?? throw new ArgumentNullException(nameof(levelRegistry));
        _indexPrefix = indexPrefix;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _ownsClient = httpClient is null;
    }

    public override void Log(LogEvent logEvent) {
        ArgumentNullException.ThrowIfNull(logEvent);
        LogBatch([logEvent]);
    }

    public void LogBatch(System.Collections.Generic.IReadOnlyList<LogEvent> events) {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { SkipValidation = true });

        foreach (var logEvent in events)
        {
            var indexName = $"{_indexPrefix}-{logEvent.TimeUtc:yyyy.MM.dd}";

            // Bulk API action line
            buffer.Write(Encoding.UTF8.GetBytes($"{{\"index\":{{\"_index\":\"{indexName}\"}}}}\n"));

            // Document
            writer.Reset();
            WriteDocument(writer, logEvent);
            writer.Flush();
            buffer.Write(Encoding.UTF8.GetBytes("\n"));
        }

        var content = new ByteArrayContent(buffer.WrittenSpan.ToArray());
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ndjson");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/_bulk") { Content = content };
        using var response = _httpClient.Send(request);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() {
        if (_ownsClient) _httpClient.Dispose();
    }

    // Shape matches Elasticsearch's common Ecs-adjacent layout: @timestamp
    // at the root, severity on its own field, then two nested objects
    // (properties, context) so Elasticsearch's indexing rules treat user
    // property names and the event's ambient context as separate
    // namespaces. Collocating them would let a user property named
    // "exception" collide with a real exception in context — keeping
    // them nested removes the ambiguity.
    private void WriteDocument(Utf8JsonWriter writer, LogEvent logEvent) {
        var registeredLevel = _levelRegistry.GetRegisteredLevel(logEvent.Level);

        writer.WriteStartObject();
        writer.WriteString("@timestamp", logEvent.TimeUtc.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteString("level", registeredLevel.Level.DisplayName);
        writer.WriteString(MMP.Herald.Services.JsonOutputKeys.LevelKey, registeredLevel.Level.Key);
        writer.WriteString("category", logEvent.Category.Value);
        writer.WriteString("message", logEvent.Message);
        writer.WriteString(MMP.Herald.Services.JsonOutputKeys.MessageTemplate, logEvent.MessageTemplate);

        if (logEvent.Properties.Count > 0)
        {
            writer.WriteStartObject("properties");
            foreach (var prop in logEvent.Properties)
            {
                writer.WriteString(prop.Name, prop.ResolvedValue?.ToString() ?? "null");
            }
            writer.WriteEndObject();
        }

        if (logEvent.Context.Count > 0)
        {
            // Context entries that carry an Exception expand into a typed
            // sub-object (type / message / stack) so Elasticsearch maps
            // the fields independently — Kibana's exception-tracking UI
            // keys off exception.type and exception.message, so flattening
            // to a single string would lose the per-field filter surface.
            // Other context values stringify to a flat field.
            writer.WriteStartObject("context");
            foreach (var pair in logEvent.Context)
            {
                if (pair.Value is Exception ex)
                {
                    writer.WriteStartObject(pair.Key);
                    writer.WriteString("type", ex.GetType().FullName ?? ex.GetType().Name);
                    writer.WriteString("message", ex.Message);
                    writer.WriteString(MMP.Herald.Services.JsonOutputKeys.StackTrace, ex.StackTrace ?? "");
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WriteString(pair.Key, pair.Value?.ToString() ?? "null");
                }
            }
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }
}
