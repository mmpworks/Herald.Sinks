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

namespace Herald.Sinks.Loki;

/// <summary>
/// Sink that posts log events to Grafana Loki's <c>/loki/api/v1/push</c>
/// endpoint. Loki is a label-driven log aggregator — the wire shape
/// separates a small set of indexed labels (used for retrieval) from
/// the log line payload (stored compressed). Events sharing the same
/// label set form a stream; streams are what Loki queries against.
/// </summary>
/// <remarks>
/// <para>
/// Wire format: <c>POST {endpoint}/loki/api/v1/push</c> with body
/// <code>
/// {
///   "streams": [
///     {
///       "stream": { "label": "value", ... },
///       "values": [ [ "nanosecond_timestamp_string", "log line" ], ... ]
///     }
///   ]
/// }
/// </code>
/// </para>
/// <para>
/// Labels come from two sources. Static labels are set once at sink
/// construction (common: <c>app</c>, <c>env</c>, <c>instance</c>). Event
/// labels are derived per-event from the level and category. Label
/// cardinality matters in Loki — each distinct label set creates a new
/// stream index — so properties are NOT promoted to labels by default;
/// they land inside the log-line JSON instead.
/// </para>
/// <para>
/// Auth: optional HTTP basic auth via <see cref="System.Net.Http.Headers.AuthenticationHeaderValue"/>,
/// or an optional bearer token for Grafana Cloud. Self-hosted Loki
/// installations behind a reverse proxy that handles auth externally
/// can leave both unset.
/// </para>
/// </remarks>
public sealed class LokiLogSink : ILogger, IBatchedLogSink, IDisposable
{
    public const string PushPathSuffix = "loki/api/v1/push";

    private readonly Uri _endpoint;
    private readonly IReadOnlyDictionary<string, string>? _staticLabels;
    private readonly string? _bearerToken;
    private readonly string? _basicAuth;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public LokiLogSink(
        string endpoint,
        IReadOnlyDictionary<string, string>? staticLabels = null,
        string? bearerToken = null,
        string? basicAuthUser = null,
        string? basicAuthPassword = null,
        HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        _endpoint = ResolveEndpoint(endpoint);
        _staticLabels = NormaliseLabels(staticLabels);
        _bearerToken = Nullify(bearerToken);
        _basicAuth = BuildBasicAuth(basicAuthUser, basicAuthPassword);
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

        if (_bearerToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _bearerToken);
        }
        else if (_basicAuth is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _basicAuth);
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

    // -- Payload assembly --

    private string BuildPayload(IReadOnlyList<LogEvent> events)
    {
        // Group events by label set so multi-level / multi-category
        // batches produce the minimum number of Loki streams. A single
        // pipeline posting a mix of Info + Warn events for category
        // "auth" + "billing" produces up to four streams per flush —
        // but that's 4, not 4*batchSize.
        var groups = new Dictionary<string, StreamGroup>(StringComparer.Ordinal);
        foreach (var evt in events)
        {
            var key = LabelKey(evt);
            if (!groups.TryGetValue(key, out var group))
            {
                group = new StreamGroup(BuildLabelsForEvent(evt));
                groups[key] = group;
            }
            group.Events.Add(evt);
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("streams");
            foreach (var group in groups.Values)
            {
                WriteStream(writer, group);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteStream(Utf8JsonWriter writer, StreamGroup group)
    {
        writer.WriteStartObject();

        writer.WriteStartObject("stream");
        foreach (var pair in group.Labels)
        {
            writer.WriteString(pair.Key, pair.Value);
        }
        writer.WriteEndObject(); // stream

        writer.WriteStartArray("values");
        foreach (var evt in group.Events)
        {
            writer.WriteStartArray();
            // Loki expects nanosecond-precision unix timestamp as a
            // string, not a number — a JavaScript i53 would lose the low
            // nanosecond bits. Ticks are 100-ns; multiply by 100 to
            // reach nanoseconds, then emit as decimal string.
            writer.WriteStringValue((evt.TimeUtc.ToUnixTimeMilliseconds() * 1_000_000L).ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteStringValue(BuildLogLine(evt));
            writer.WriteEndArray();
        }
        writer.WriteEndArray(); // values

        writer.WriteEndObject(); // stream entry
    }

    // -- Label construction --

    private IReadOnlyDictionary<string, string> BuildLabelsForEvent(LogEvent evt)
    {
        // Static labels + canonical per-event labels (level, category).
        // Property values stay OUT of the label set to keep Loki's
        // cardinality bounded — one operator mistake promoting userId
        // to a label would explode the index. Properties ride in the
        // log-line JSON instead.
        var labels = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (_staticLabels is not null)
        {
            foreach (var pair in _staticLabels)
            {
                labels[SanitizeLabelName(pair.Key)] = pair.Value ?? string.Empty;
            }
        }
        labels["level"] = evt.Level.Key;
        labels["category"] = evt.Category.Value;
        return labels;
    }

    private string LabelKey(LogEvent evt)
    {
        var labels = BuildLabelsForEvent(evt);
        var sb = new StringBuilder();
        foreach (var pair in labels)
        {
            sb.Append(pair.Key).Append('=').Append(pair.Value).Append(';');
        }
        return sb.ToString();
    }

    private static string SanitizeLabelName(string name)
    {
        // Loki label names must match [a-zA-Z_][a-zA-Z0-9_]*. Operator
        // typos are normalised silently so the push doesn't 400;
        // upstream the caller sees the same label they set, just with
        // invalid characters replaced with underscore.
        if (string.IsNullOrEmpty(name)) return "_";
        var sb = new StringBuilder(name.Length);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            var ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_'
                || (i > 0 && c >= '0' && c <= '9');
            sb.Append(ok ? c : '_');
        }
        if (sb.Length == 0 || (sb[0] >= '0' && sb[0] <= '9'))
        {
            sb.Insert(0, '_');
        }
        return sb.ToString();
    }

    // -- Log line --

    private static string BuildLogLine(LogEvent evt)
    {
        // The log line is a JSON object so Loki's JSON parser can pick
        // out fields for LogQL queries (`| json`). Keeping the line
        // structured is the norm for Grafana Cloud dashboards.
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("message", evt.Message);
            writer.WriteString("messageTemplate", evt.MessageTemplate);

            WriteException(writer, evt);
            WriteProperties(writer, evt);

            writer.WriteEndObject();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
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
            "message", "messageTemplate", "exception"
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

    // -- Config helpers --

    private static Uri ResolveEndpoint(string endpoint)
    {
        var uri = new Uri(endpoint, UriKind.Absolute);
        if (uri.AbsolutePath.Contains("/loki/api/v1/push", StringComparison.OrdinalIgnoreCase))
        {
            return uri;
        }
        var trimmed = endpoint.TrimEnd('/');
        return new Uri($"{trimmed}/{PushPathSuffix}", UriKind.Absolute);
    }

    private static IReadOnlyDictionary<string, string>? NormaliseLabels(
        IReadOnlyDictionary<string, string>? labels)
    {
        if (labels is null || labels.Count == 0) return null;
        var normalised = new Dictionary<string, string>(labels.Count, StringComparer.Ordinal);
        foreach (var pair in labels)
        {
            if (string.IsNullOrWhiteSpace(pair.Key)) continue;
            normalised[pair.Key] = pair.Value ?? string.Empty;
        }
        return normalised.Count == 0 ? null : normalised;
    }

    private static string? BuildBasicAuth(string? user, string? password)
    {
        if (string.IsNullOrEmpty(user)) return null;
        var raw = $"{user}:{password ?? string.Empty}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    private static string? Nullify(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed class StreamGroup
    {
        public StreamGroup(IReadOnlyDictionary<string, string> labels)
        {
            Labels = labels;
        }
        public IReadOnlyDictionary<string, string> Labels { get; }
        public List<LogEvent> Events { get; } = new();
    }
}
