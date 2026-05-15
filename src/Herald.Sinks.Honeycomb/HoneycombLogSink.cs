// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;

namespace Herald.Sinks.Honeycomb;

/// <summary>
/// Sink that posts log events to Honeycomb's batch ingest endpoint.
/// Honeycomb is a distributed-tracing-flavoured observability platform
/// that accepts arbitrary structured events keyed by a "dataset" name;
/// each event is a flat JSON object with an optional timestamp and a
/// numeric <c>samplerate</c> the server multiplies back out to recover
/// the original event volume.
/// </summary>
/// <remarks>
/// <para>
/// Wire format: <c>POST https://api.honeycomb.io/1/batch/{dataset}</c>
/// with body shape
/// <code>
/// [ { "time": "2025-...Z", "samplerate": 1, "data": { ...flat fields... } }, ... ]
/// </code>
/// Auth is <c>X-Honeycomb-Team: {api_key}</c>. A classic API key scopes
/// to one team; an ingest-only key scopes to one dataset. The dataset
/// name in the URL determines which Honeycomb dataset receives the events.
/// </para>
/// <para>
/// The <c>data</c> object is flat — no nested properties. Herald's
/// <see cref="LogEvent"/> is largely flat anyway (properties +
/// top-level fields), so this maps cleanly. Callers who need nested
/// structures should flatten them upstream or accept that Honeycomb
/// will store the serialised form as a string.
/// </para>
/// <para>
/// Self-hosted Honeycomb installations (Refinery or Honeycomb Enterprise
/// on-prem) follow the same URL shape; pass the full URL including
/// dataset segment to the constructor to override the public host.
/// </para>
/// </remarks>
public sealed class HoneycombLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable
{
    public const string PublicHost = "https://api.honeycomb.io";
    public const string BatchPathPrefix = "1/batch";

    private readonly Uri _endpoint;
    private readonly string _apiKey;
    private readonly double _sampleRate;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public HoneycombLogSink(
        string apiKey,
        string dataset,
        string? endpoint = null,
        double sampleRate = 1.0,
        HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset);
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate),
            sampleRate, "sample rate must be positive (1 = keep every event).");

        _apiKey = apiKey;
        _sampleRate = sampleRate;
        _endpoint = ResolveEndpoint(endpoint, dataset);
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        LogBatch([logEvent]);
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        var payload = BuildBatchPayload(events);

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Honeycomb-Team", _apiKey);

        using var response = _httpClient.Send(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    // -- Payload assembly --

    private string BuildBatchPayload(IReadOnlyList<LogEvent> events)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var evt in events)
            {
                WriteBatchItem(writer, evt);
            }
            writer.WriteEndArray();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private void WriteBatchItem(Utf8JsonWriter writer, LogEvent evt)
    {
        writer.WriteStartObject();

        // Honeycomb expects RFC 3339 with millisecond precision minimum.
        // "O" on DateTimeOffset produces ISO 8601 with ticks — the extra
        // precision is accepted and preserves nanosecond fidelity when
        // the source clock has it.
        writer.WriteString("time", evt.TimeUtc.ToUniversalTime().ToString("O"));

        // samplerate tells the Honeycomb server to multiply event counts
        // back out. Default 1 (keep every event); callers using an
        // upstream sampler should pass the rate here too so aggregates
        // match the original volume.
        writer.WriteNumber("samplerate", _sampleRate);

        writer.WriteStartObject("data");
        writer.WriteString("message", evt.Message);
        writer.WriteString("messageTemplate", evt.MessageTemplate);
        writer.WriteString("level", evt.Level.Key);
        writer.WriteString("category", evt.Category.Value);

        WriteException(writer, evt);
        WriteProperties(writer, evt);

        writer.WriteEndObject(); // data
        writer.WriteEndObject(); // batch item
    }

    private static void WriteException(Utf8JsonWriter writer, LogEvent evt)
    {
        if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value)
            && value is Exception ex)
        {
            writer.WriteString("exception", ex.ToString());
            writer.WriteString("exception.type", ex.GetType().FullName ?? ex.GetType().Name);
        }
    }

    private static void WriteProperties(Utf8JsonWriter writer, LogEvent evt)
    {
        // Reserved names defined by the data-flat shape above. Skip them
        // on the property-walk so caller-supplied "level" / "message"
        // properties do not collide with the canonical envelope fields.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "message", "messageTemplate", "level", "category",
            "exception", "exception.type"
        };

        foreach (var property in evt.Properties)
        {
            if (seen.Add(property.Name))
            {
                WriteValue(writer, property.Name, property.ResolvedValue);
            }
        }

        foreach (var pair in evt.Context)
        {
            if (pair.Key == LogContextKeys.Exception) continue;
            if (seen.Add(pair.Key))
            {
                WriteValue(writer, pair.Key, pair.Value);
            }
        }
    }

    private static void WriteValue(Utf8JsonWriter writer, string name, object? value)
    {
        switch (value)
        {
            case null: writer.WriteNull(name); break;
            case string s: writer.WriteString(name, s); break;
            case bool b: writer.WriteBoolean(name, b); break;
            case int i: writer.WriteNumber(name, i); break;
            case long l: writer.WriteNumber(name, l); break;
            case double d: writer.WriteNumber(name, d); break;
            case decimal m: writer.WriteNumber(name, m); break;
            case DateTime dt: writer.WriteString(name, dt.ToString("O")); break;
            case DateTimeOffset dto: writer.WriteString(name, dto.ToString("O")); break;
            case Guid g: writer.WriteString(name, g.ToString("D")); break;
            default: writer.WriteString(name, value.ToString() ?? ""); break;
        }
    }

    private static Uri ResolveEndpoint(string? endpointOverride, string dataset)
    {
        // Accept three endpoint shapes without surprising the caller:
        //   null              → https://api.honeycomb.io/1/batch/{dataset}
        //   host-root override → {override}/1/batch/{dataset}
        //   full URL override  → used verbatim (for Refinery / Enterprise)
        if (string.IsNullOrWhiteSpace(endpointOverride))
        {
            return new Uri($"{PublicHost}/{BatchPathPrefix}/{Uri.EscapeDataString(dataset)}", UriKind.Absolute);
        }

        var uri = new Uri(endpointOverride, UriKind.Absolute);
        if (uri.AbsolutePath.Contains("/batch/", StringComparison.Ordinal))
        {
            // Caller supplied the full /1/batch/{dataset} path already.
            return uri;
        }

        var root = endpointOverride.TrimEnd('/');
        return new Uri($"{root}/{BatchPathPrefix}/{Uri.EscapeDataString(dataset)}", UriKind.Absolute);
    }
}
