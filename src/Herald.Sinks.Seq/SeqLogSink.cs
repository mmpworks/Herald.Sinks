// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;

namespace Herald.Sinks.Seq;

/// <summary>
/// Sink that posts log events to a Seq server as newline-delimited CLEF
/// (Compact Log Event Format) over HTTP. The canonical structured-log
/// viewer in the Serilog ecosystem; the wire format matches what
/// <c>Serilog.Sinks.Seq</c> ships so teams with an existing Seq deploy
/// can switch to Herald without touching their log store.
/// </summary>
/// <remarks>
/// <para>
/// POSTs to <c>{server}/api/events/raw?clef</c> with content-type
/// <c>application/vnd.serilog.clef</c>. Batches are one HTTP request
/// with a newline-separated body — Seq parses each line as an
/// independent CLEF event. The API key (optional) rides in the
/// <c>X-Seq-ApiKey</c> header.
/// </para>
/// <para>
/// Implements <see cref="IBatchedLogSink"/>. Batch management is the
/// caller's job — wire a batching decorator upstream if you want
/// throughput amortisation.
/// </para>
/// </remarks>
public sealed class SeqLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable
{
    /// <summary>
    /// The CLEF content type Seq accepts at <c>/api/events/raw?clef</c>.
    /// </summary>
    public const string ClefMediaType = "application/vnd.serilog.clef";

    private const string RawEventsPath = "api/events/raw?clef";

    private readonly Uri _endpoint;
    private readonly string? _apiKey;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public SeqLogSink(
        string serverUrl,
        string? apiKey = null,
        HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverUrl);

        var normalised = serverUrl.EndsWith('/') ? serverUrl : serverUrl + "/";
        _endpoint = new Uri(new Uri(normalised, UriKind.Absolute), RawEventsPath);
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
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

        var payload = BuildClefPayload(events);

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(ClefMediaType);

        if (_apiKey is not null)
        {
            request.Headers.Add("X-Seq-ApiKey", _apiKey);
        }

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

    // ── CLEF payload ──────────────────────────────────────────────

    private static string BuildClefPayload(IReadOnlyList<LogEvent> events)
    {
        // CLEF is newline-delimited JSON: one event per line, no
        // surrounding array. One MemoryStream fills the whole batch so
        // Seq receives it as one request body.
        using var stream = new MemoryStream();

        var writerOptions = new JsonWriterOptions { SkipValidation = false };
        foreach (var evt in events)
        {
            using (var writer = new Utf8JsonWriter(stream, writerOptions))
            {
                WriteClefEvent(writer, evt);
                writer.Flush();
            }
            stream.WriteByte((byte)'\n');
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteClefEvent(Utf8JsonWriter writer, LogEvent evt)
    {
        writer.WriteStartObject();

        writer.WriteString("@t", evt.TimeUtc.UtcDateTime.ToString("O"));
        writer.WriteString("@m", evt.Message);

        if (!string.IsNullOrEmpty(evt.MessageTemplate) && evt.MessageTemplate != evt.Message)
        {
            writer.WriteString("@mt", evt.MessageTemplate);
        }

        var levelName = SeqLevelMapper.MapLevel(evt.Level);
        if (levelName is not null)
        {
            writer.WriteString("@l", levelName);
        }

        WriteException(writer, evt);
        WriteEventId(writer, evt);

        // Category goes in as a top-level property too — Seq's filter
        // syntax treats it like any other structured field, which is
        // exactly the discoverability behaviour Serilog users expect.
        writer.WriteString("SourceContext", evt.Category.Value);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "@t", "@m", "@mt", "@l", "@x", "@i", "SourceContext"
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

        writer.WriteEndObject();
    }

    private static void WriteException(Utf8JsonWriter writer, LogEvent evt)
    {
        if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value)
            && value is Exception ex)
        {
            writer.WriteString("@x", ex.ToString());
        }
    }

    private static void WriteEventId(Utf8JsonWriter writer, LogEvent evt)
    {
        if (evt.EventId is { } id)
        {
            // CLEF uses @i for event id — a stable identifier for the
            // specific message template. Id.Id as a hex string matches
            // Serilog's convention.
            writer.WriteString("@i", id.Id.ToString("X"));
        }
    }

    private static void WriteValue(Utf8JsonWriter writer, string name, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNull(name);
                break;
            case string s:
                writer.WriteString(name, s);
                break;
            case bool b:
                writer.WriteBoolean(name, b);
                break;
            case int i:
                writer.WriteNumber(name, i);
                break;
            case long l:
                writer.WriteNumber(name, l);
                break;
            case double d:
                writer.WriteNumber(name, d);
                break;
            case decimal m:
                writer.WriteNumber(name, m);
                break;
            case DateTime dt:
                writer.WriteString(name, dt.ToString("O"));
                break;
            case DateTimeOffset dto:
                writer.WriteString(name, dto.ToString("O"));
                break;
            case Guid g:
                writer.WriteString(name, g.ToString("D"));
                break;
            default:
                writer.WriteString(name, value.ToString() ?? "");
                break;
        }
    }
}
