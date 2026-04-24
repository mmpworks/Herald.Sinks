// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
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
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;

namespace Herald.Sinks.Splunk;

/// <summary>
/// Sink that posts log events to Splunk's HTTP Event Collector (HEC)
/// endpoint. Splunk is the dominant enterprise log aggregator;
/// <c>Serilog.Sinks.Splunk</c> is the Serilog equivalent, and this
/// sink matches its wire shape so teams already running Splunk can
/// swap loggers without touching the collector.
/// </summary>
/// <remarks>
/// <para>
/// HEC wire format: each line is one JSON object wrapping an
/// <c>event</c> value with Splunk-specific envelope fields
/// (<c>time</c>, <c>host</c>, <c>source</c>, <c>sourcetype</c>,
/// <c>index</c>). A batched request is multiple such objects
/// concatenated — no outer array. Auth is <c>Authorization: Splunk
/// {token}</c>.
/// </para>
/// <para>
/// Endpoint path defaults to <c>/services/collector/event</c>. Callers
/// pointing at Splunk Cloud or a non-default HEC path can pass the
/// full URL to the constructor.
/// </para>
/// </remarks>
public sealed class SplunkHecLogSink : ILogger, IBatchedLogSink, IDisposable
{
    public const string DefaultHecPath = "services/collector/event";

    private readonly Uri _endpoint;
    private readonly string _token;
    private readonly string? _host;
    private readonly string? _source;
    private readonly string? _sourceType;
    private readonly string? _index;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public SplunkHecLogSink(
        string hecUrl,
        string token,
        string? host = null,
        string? source = null,
        string? sourceType = null,
        string? index = null,
        HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hecUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        _endpoint = ResolveEndpoint(hecUrl);
        _token = token;
        _host = Nullify(host);
        _source = Nullify(source);
        _sourceType = Nullify(sourceType);
        _index = Nullify(index);
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
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

        var payload = BuildHecPayload(events);

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Splunk", _token);

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

    // ── HEC payload ──────────────────────────────────────────────

    private string BuildHecPayload(IReadOnlyList<LogEvent> events)
    {
        // HEC multi-event body: JSON objects concatenated. No outer
        // array, no newline strictly required — but writers that add
        // one between objects are universally accepted and easier to
        // debug against tcpdump.
        using var stream = new MemoryStream();

        foreach (var evt in events)
        {
            using (var writer = new Utf8JsonWriter(stream))
            {
                WriteHecEvent(writer, evt);
                writer.Flush();
            }
            stream.WriteByte((byte)'\n');
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private void WriteHecEvent(Utf8JsonWriter writer, LogEvent evt)
    {
        writer.WriteStartObject();

        // Splunk expects seconds-since-epoch for HEC timestamp. Use
        // double so sub-second precision survives the wire.
        writer.WriteNumber("time",
            evt.TimeUtc.ToUnixTimeMilliseconds() / 1000.0);

        if (_host is not null) writer.WriteString("host", _host);
        if (_source is not null) writer.WriteString("source", _source);
        if (_sourceType is not null) writer.WriteString("sourcetype", _sourceType);
        if (_index is not null) writer.WriteString("index", _index);

        writer.WriteStartObject("event");
        writer.WriteString("message", evt.Message);
        writer.WriteString("messageTemplate", evt.MessageTemplate);
        writer.WriteString("severity", SplunkLevelMapper.MapLevel(evt.Level));
        writer.WriteString("category", evt.Category.Value);

        WriteException(writer, evt);
        WriteProperties(writer, evt);

        writer.WriteEndObject(); // event
        writer.WriteEndObject(); // envelope
    }

    private static void WriteException(Utf8JsonWriter writer, LogEvent evt)
    {
        if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value)
            && value is Exception ex)
        {
            writer.WriteString("exception", ex.ToString());
        }
    }

    private static void WriteProperties(Utf8JsonWriter writer, LogEvent evt)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "message", "messageTemplate", "severity",
            "category", "exception"
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

    private static Uri ResolveEndpoint(string hecUrl)
    {
        // Caller gave us either a full HEC URL
        // (https://host:8088/services/collector/event) or the server
        // root (https://host:8088). If the path is missing, append
        // the default.
        var uri = new Uri(hecUrl, UriKind.Absolute);
        if (string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/")
        {
            var normalised = hecUrl.EndsWith('/') ? hecUrl : hecUrl + "/";
            return new Uri(new Uri(normalised, UriKind.Absolute), DefaultHecPath);
        }
        return uri;
    }

    private static string? Nullify(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
