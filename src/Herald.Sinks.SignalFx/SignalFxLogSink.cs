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

namespace Herald.Sinks.SignalFx;

/// <summary>
/// Sink that posts log events to SignalFx / Splunk Observability Cloud's
/// HTTP ingest endpoint. SignalFx routes by realm (<c>us0</c>, <c>us1</c>,
/// <c>eu0</c>, etc.) so callers supply either a realm name or a full
/// ingest URL.
/// </summary>
/// <remarks>
/// <para>
/// Wire format: <c>POST https://ingest.{realm}.signalfx.com/v2/log</c>
/// with header <c>X-SF-Token: {access_token}</c>. Body is a JSON array
/// where each entry carries a timestamp, the rendered message, the
/// level, and any properties. SignalFx indexes the fields for its
/// log search surface; properties appear as facets.
/// </para>
/// <para>
/// The <c>dimensions</c> object is the low-cardinality label bag
/// SignalFx uses for correlation with metrics. Configured once at
/// sink construction — environment, service, version, host — and
/// emitted on every event. Per-event properties go in the flat
/// payload, not dimensions, because dimension cardinality costs are
/// non-trivial on the SignalFx side.
/// </para>
/// <para>
/// Self-hosted deployments (Splunk Observability on-prem) follow the
/// same URL shape; pass the full URL to override the public realm host.
/// </para>
/// </remarks>
public sealed class SignalFxLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable, INetworkSink
{
    public const string PathSuffix = "v2/log";

    private readonly Uri _endpoint;
    private readonly string _accessToken;
    private readonly IReadOnlyDictionary<string, string>? _dimensions;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public SignalFxLogSink(
        string accessToken,
        string? endpoint = null,
        string? realm = null,
        IReadOnlyDictionary<string, string>? dimensions = null,
        HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        _accessToken = accessToken;
        _dimensions = NormaliseDimensions(dimensions);
        _endpoint = ResolveEndpoint(endpoint, realm);
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

        var payload = BuildPayload(events);

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-SF-Token", _accessToken);

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

        // SignalFx expects unix milliseconds on the timestamp field.
        writer.WriteNumber("timestamp", evt.TimeUtc.ToUnixTimeMilliseconds());
        writer.WriteString("message", evt.Message);
        writer.WriteString("level", evt.Level.Key);
        writer.WriteString("category", evt.Category.Value);
        writer.WriteString("messageTemplate", evt.MessageTemplate);

        if (_dimensions is not null && _dimensions.Count > 0)
        {
            writer.WriteStartObject("dimensions");
            foreach (var pair in _dimensions)
            {
                writer.WriteString(pair.Key, pair.Value);
            }
            writer.WriteEndObject();
        }

        WriteException(writer, evt);
        WriteProperties(writer, evt);

        writer.WriteEndObject();
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
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "timestamp", "message", "level", "category", "messageTemplate",
            "dimensions", "exception", "exception.type"
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

    private static Uri ResolveEndpoint(string? endpointOverride, string? realm)
    {
        // Three shapes accepted:
        //   1. Full URL with path → used verbatim (self-hosted, custom path)
        //   2. Host-only URL → append /v2/log
        //   3. Realm name ("us1") → https://ingest.us1.signalfx.com/v2/log
        if (!string.IsNullOrWhiteSpace(endpointOverride))
        {
            var uri = new Uri(endpointOverride, UriKind.Absolute);
            if (uri.AbsolutePath.Contains("/v2/log", StringComparison.OrdinalIgnoreCase))
            {
                return uri;
            }
            var trimmed = endpointOverride.TrimEnd('/');
            return new Uri($"{trimmed}/{PathSuffix}", UriKind.Absolute);
        }

        var effectiveRealm = string.IsNullOrWhiteSpace(realm) ? "us0" : realm;
        return new Uri($"https://ingest.{effectiveRealm}.signalfx.com/{PathSuffix}", UriKind.Absolute);
    }

    private static IReadOnlyDictionary<string, string>? NormaliseDimensions(
        IReadOnlyDictionary<string, string>? dims)
    {
        if (dims is null || dims.Count == 0) return null;
        var normalised = new Dictionary<string, string>(dims.Count, StringComparer.Ordinal);
        foreach (var pair in dims)
        {
            if (string.IsNullOrWhiteSpace(pair.Key)) continue;
            normalised[pair.Key] = pair.Value ?? string.Empty;
        }
        return normalised.Count == 0 ? null : normalised;
    }
}
