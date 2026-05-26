// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.Coralogix;

/// <summary>
/// Sink that ships log events to Coralogix via the bulk-logs ingest
/// endpoint. Private-key auth in the body envelope; HTTP-only.
/// </summary>
/// <remarks>
/// <para>
/// <b>Property preservation (opt-in, default off).</b> When
/// <c>preserveProperties</c> is true, the per-entry <c>text</c> field is
/// emitted as a JSON object string carrying the message plus each event
/// property (type-preserved), instead of the bare message. Coralogix
/// auto-extracts JSON keys from <c>text</c> into structured metadata, so
/// this is the Coralogix-idiomatic way to ship richer data. The
/// timestamp/severity/category envelope fields are unchanged either way.
/// When false, <c>text</c> stays the plain message — today's behaviour.
/// Preservation ships ALL event properties downstream — ensure your
/// redaction/enrichment pipeline runs before the sink.
/// </para>
/// </remarks>
public sealed class CoralogixLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable, INetworkSink
{
    private const string DefaultEndpoint = "https://ingress.coralogix.com/api/v1/logs";

    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly string _privateKey;
    private readonly string _applicationName;
    private readonly string _subsystemName;
    private readonly bool _ownsHttp;
    private readonly bool _preserveProperties;

    public CoralogixLogSink(string privateKey, string applicationName, string subsystemName,
        string? endpoint = null, HttpClient? httpClient = null,
        bool preserveProperties = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(subsystemName);
        _privateKey = privateKey;
        _applicationName = applicationName;
        _subsystemName = subsystemName;
        _endpoint = new Uri(endpoint ?? DefaultEndpoint, UriKind.Absolute);
        _ownsHttp = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _preserveProperties = preserveProperties;
    }

    public override void Log(LogEvent logEvent) => LogBatch(new[] { logEvent });

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        var body = BuildBody(events);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = _http.PostAsync(_endpoint, content).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }

    private string BuildBody(IReadOnlyList<LogEvent> events)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("privateKey", _privateKey);
            writer.WriteString("applicationName", _applicationName);
            writer.WriteString("subsystemName", _subsystemName);

            writer.WriteStartArray("logEntries");
            foreach (var evt in events)
            {
                writer.WriteStartObject();
                writer.WriteNumber("timestamp", evt.TimeUtc.ToUnixTimeMilliseconds());
                writer.WriteNumber("severity", MapSeverity(evt.Level.Key));
                writer.WriteString("category", evt.Category.Value);
                writer.WriteString("text", BuildText(evt));
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    // Off: the bare rendered message (today's behaviour). On: a JSON object
    // string carrying the message plus each property, type-preserved.
    // Coralogix parses a JSON text field into structured metadata facets.
    private string BuildText(LogEvent evt)
    {
        if (!_preserveProperties)
        {
            return evt.Message ?? string.Empty;
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("message", evt.Message ?? string.Empty);
            foreach (var property in evt.Properties)
            {
                // "message" is reserved above; skip a same-named property
                // (seen-style guard, same pattern as the other sinks).
                if (string.Equals(property.Name, "message", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                WriteValue(writer, property.Name, property.ResolvedValue);
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
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
            case float f: writer.WriteNumber(name, f); break;
            case decimal m: writer.WriteNumber(name, m); break;
            case DateTime dt: writer.WriteString(name, dt.ToString("O", CultureInfo.InvariantCulture)); break;
            case DateTimeOffset dto: writer.WriteString(name, dto.ToString("O", CultureInfo.InvariantCulture)); break;
            case Guid g: writer.WriteString(name, g.ToString("D")); break;
            default: writer.WriteString(name, value.ToString() ?? ""); break;
        }
    }

    private static int MapSeverity(string levelKey) => levelKey switch
    {
        "trace" or "debug" => 1,
        "info" or "notice" => 3,
        "warn" => 4,
        "error" => 5,
        "critical" or "security" => 6,
        _ => 3,
    };
}
