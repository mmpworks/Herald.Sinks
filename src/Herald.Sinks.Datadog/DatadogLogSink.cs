// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using MMP.Herald;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;

namespace Herald.Sinks.Datadog;

/// <summary>
/// Sink that posts log events to Datadog's HTTP log intake. The same
/// wire shape works in two deployment patterns:
/// <list type="bullet">
///   <item>
///     <b>Direct to Datadog.</b> Point the endpoint at the site-specific
///     ingest host (<c>https://http-intake.logs.datadoghq.com</c>,
///     <c>https://http-intake.logs.datadoghq.eu</c>, etc.) and supply the
///     Datadog API key. This is the low-infrastructure setup for small
///     deployments.
///   </item>
///   <item>
///     <b>Via the Datadog Agent.</b> Point the endpoint at a local
///     Datadog Agent configured with an HTTP log intake listener
///     (typically <c>http://localhost:8126</c> or a separate logs port).
///     The Agent adds its own metadata, handles batching and retries,
///     and forwards to Datadog. Recommended for anything running on a
///     host that already runs the Agent for traces or metrics.
///   </item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// Wire format: <c>POST {endpoint}/api/v2/logs</c> (the <c>/api/v2/logs</c>
/// segment is appended if the supplied endpoint does not already include
/// it). Body is a JSON array where each entry carries the Datadog
/// reserved fields <c>ddsource</c>, <c>ddtags</c>, <c>hostname</c>,
/// <c>service</c>, <c>message</c>, <c>status</c>, plus any caller
/// properties. Auth is header <c>DD-API-KEY</c>.
/// </para>
/// <para>
/// <c>status</c> maps Herald levels to Datadog's known log-status values
/// (<c>debug</c>, <c>info</c>, <c>notice</c>, <c>warn</c>, <c>error</c>,
/// <c>critical</c>, <c>alert</c>, <c>emergency</c>). Datadog treats
/// unknown statuses as <c>info</c>, so the mapping errs on the side of
/// landing inside the known set.
/// </para>
/// <para>
/// <c>ddtags</c> is a comma-separated list of <c>key:value</c> tags.
/// Common tags (<c>env:prod</c>, <c>service:myapp</c>, <c>version:1.2.3</c>)
/// are configured once at sink construction; the Herald category is added
/// per event as <c>category:{name}</c>.
/// </para>
/// </remarks>
public sealed class DatadogLogSink : ILogger, IBatchedLogSink, IDisposable
{
    public const string PublicUsEndpoint = "https://http-intake.logs.datadoghq.com";
    public const string LogsPathSuffix = "api/v2/logs";

    private readonly Uri _endpoint;
    private readonly string _apiKey;
    private readonly string _service;
    private readonly string _ddsource;
    private readonly string? _hostname;
    private readonly string? _staticTags;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public DatadogLogSink(
        string apiKey,
        string service,
        string? endpoint = null,
        string? ddsource = null,
        string? hostname = null,
        IReadOnlyDictionary<string, string>? staticTags = null,
        HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(service);

        _apiKey = apiKey;
        _service = service;
        _ddsource = string.IsNullOrWhiteSpace(ddsource) ? "herald" : ddsource;
        _hostname = Nullify(hostname);
        _staticTags = BuildStaticTags(staticTags);
        _endpoint = ResolveEndpoint(endpoint);
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

        var payload = BuildPayload(events);

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("DD-API-KEY", _apiKey);

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

    // -- Payload --

    private string BuildPayload(IReadOnlyList<LogEvent> events)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var evt in events)
            {
                WriteLog(writer, evt);
            }
            writer.WriteEndArray();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private void WriteLog(Utf8JsonWriter writer, LogEvent evt)
    {
        writer.WriteStartObject();

        // Datadog reserved fields first — its indexer pays special
        // attention to these and routes them into the canonical log
        // attributes. Everything else lands in an "attributes" bag but
        // remains queryable via the log-explorer.
        writer.WriteString("message", evt.Message);
        writer.WriteString("ddsource", _ddsource);
        writer.WriteString("service", _service);
        writer.WriteString("status", MapStatus(evt.Level));

        if (_hostname is not null)
        {
            writer.WriteString("hostname", _hostname);
        }

        writer.WriteString("ddtags", BuildEventTags(evt));

        // Structured extras alongside the reserved fields. Datadog
        // accepts them at the root and indexes them as facets.
        writer.WriteString("messageTemplate", evt.MessageTemplate);
        writer.WriteString("category", evt.Category.Value);
        writer.WriteString("timestamp", evt.TimeUtc.ToUniversalTime().ToString("O"));

        WriteException(writer, evt);
        WriteProperties(writer, evt);

        writer.WriteEndObject();
    }

    private string BuildEventTags(LogEvent evt)
    {
        // ddtags is a CSV string. Concatenate static tags + the event's
        // category without allocating a List when there are no static
        // tags (the common case after construction).
        var categoryTag = $"category:{evt.Category.Value}";
        if (string.IsNullOrEmpty(_staticTags)) return categoryTag;
        return $"{_staticTags},{categoryTag}";
    }

    private static string MapStatus(LogLevel level)
    {
        // Datadog's known log statuses; unknowns fall to "info" silently.
        return level.Key.ToLowerInvariant() switch
        {
            "trace" => "debug",
            "debug" => "debug",
            "info" => "info",
            "notice" => "notice",
            "warn" => "warn",
            "warning" => "warn",
            "error" => "error",
            "critical" => "critical",
            "alert" => "alert",
            "fatal" => "emergency",
            "emergency" => "emergency",
            _ => "info",
        };
    }

    private static void WriteException(Utf8JsonWriter writer, LogEvent evt)
    {
        if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value)
            && value is Exception ex)
        {
            // Datadog's error-tracking pipeline keys off error.message,
            // error.kind, and error.stack when present. Populating the
            // triple lights up error tracking in addition to plain log
            // search.
            writer.WriteString("error.message", ex.Message);
            writer.WriteString("error.kind", ex.GetType().FullName ?? ex.GetType().Name);
            writer.WriteString("error.stack", ex.ToString());
        }
    }

    private static void WriteProperties(Utf8JsonWriter writer, LogEvent evt)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "message", "ddsource", "service", "status", "hostname",
            "ddtags", "messageTemplate", "category", "timestamp",
            "error.message", "error.kind", "error.stack"
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

    private static string? BuildStaticTags(IReadOnlyDictionary<string, string>? tags)
    {
        if (tags is null || tags.Count == 0) return null;
        var sb = new StringBuilder();
        var first = true;
        foreach (var pair in tags)
        {
            if (string.IsNullOrWhiteSpace(pair.Key)) continue;
            if (!first) sb.Append(',');
            sb.Append(pair.Key).Append(':').Append(pair.Value ?? string.Empty);
            first = false;
        }
        return sb.Length == 0 ? null : sb.ToString();
    }

    private static Uri ResolveEndpoint(string? endpointOverride)
    {
        // When caller passes just a host (no path, or just /), append
        // the standard logs path. When they pass a full URL including
        // /api/v2/logs, use it verbatim — this is how operators pin to
        // a Datadog Agent's log-intake port.
        var raw = string.IsNullOrWhiteSpace(endpointOverride)
            ? PublicUsEndpoint
            : endpointOverride!;
        var uri = new Uri(raw, UriKind.Absolute);

        if (uri.AbsolutePath.Contains("/api/v2/logs", StringComparison.OrdinalIgnoreCase))
        {
            return uri;
        }

        var trimmed = raw.TrimEnd('/');
        return new Uri($"{trimmed}/{LogsPathSuffix}", UriKind.Absolute);
    }

    private static string? Nullify(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
