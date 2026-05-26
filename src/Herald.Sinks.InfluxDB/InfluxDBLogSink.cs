// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.InfluxDB;

/// <summary>
/// Sink that writes log events to InfluxDB v2 via line protocol over
/// HTTP. No SDK dependency — line protocol is small and well-documented.
/// </summary>
/// <remarks>
/// Each event becomes one line: <c>logs,level=info,category=Auth message="..." 1234567890</c>.
/// Tags are <c>level</c> and <c>category</c>; <c>message</c> rides as a field.
///
/// <para>
/// <b>Property preservation (opt-in, default off).</b> When
/// <c>preserveProperties</c> is true, arbitrary <see cref="LogEvent"/>
/// properties are carried as line-protocol <b>fields</b> (never tags),
/// type-mapped to their line-protocol field syntax. Fields are not indexed,
/// so they carry the value without the tag-cardinality cost that high-
/// cardinality data (user_id, request_id, trace_id) would inflict as tags.
/// <c>level</c> and <c>category</c> stay tags; <c>message</c> stays a field.
/// Properties whose name collides with a reserved tag/field name are skipped.
/// </para>
/// <para>
/// A soft cap (<c>preserveFieldLimit</c>, default 32) bounds how many
/// preserved fields ride per event; beyond it, extra fields are skipped and
/// a one-time diagnostic is surfaced. Preservation ships ALL event
/// properties downstream — ensure your redaction/enrichment pipeline runs
/// before the sink. InfluxDB pins a field type on first write, so mixed
/// types across events for the same key are an application-discipline
/// concern, not a sink bug.
/// </para>
/// </remarks>
public sealed class InfluxDBLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable, INetworkSink
{
    /// <summary>Default soft cap on preserved fields per event.</summary>
    public const int DefaultPreserveFieldLimit = 32;

    // Reserved line-protocol keys that preserved properties must not clash
    // with: "level"/"category" are tags, "message" is a field. Matched
    // case-insensitively (the seen-style guard, same pattern as Datadog/Splunk).
    private static readonly HashSet<string> ReservedKeys =
        new(StringComparer.OrdinalIgnoreCase) { "level", "category", "message" };

    private readonly HttpClient _http;
    private readonly Uri _writeEndpoint;
    private readonly string _token;
    private readonly bool _ownsHttp;
    private readonly bool _preserveProperties;
    private readonly int _preserveFieldLimit;

    // One-time guard so the cap diagnostic fires once per sink instance,
    // not once per event (no log spam). int + Interlocked keeps the
    // first-writer-wins semantics correct under concurrent Log calls.
    private int _capDiagnosticEmitted;

    public InfluxDBLogSink(string serverUrl, string organization, string bucket, string token,
        HttpClient? httpClient = null,
        bool preserveProperties = false,
        int preserveFieldLimit = DefaultPreserveFieldLimit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(organization);
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        _writeEndpoint = new Uri(
            $"{serverUrl.TrimEnd('/')}/api/v2/write?org={Uri.EscapeDataString(organization)}&bucket={Uri.EscapeDataString(bucket)}&precision=ms",
            UriKind.Absolute);
        _token = token;
        _ownsHttp = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _preserveProperties = preserveProperties;
        _preserveFieldLimit = preserveFieldLimit < 0 ? 0 : preserveFieldLimit;
    }

    public override void Log(LogEvent logEvent) => LogBatch(new[] { logEvent });

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        var body = BuildLineProtocol(events);
        using var content = new StringContent(body, Encoding.UTF8, "text/plain");
        using var request = new HttpRequestMessage(HttpMethod.Post, _writeEndpoint) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Token", _token);
        using var response = _http.SendAsync(request).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }

    private string BuildLineProtocol(IReadOnlyList<LogEvent> events)
    {
        var sb = new StringBuilder(events.Count * 200);
        foreach (var evt in events)
        {
            // Measurement,tag1=val,tag2=val field1="val"[,fieldN=...] timestamp_ms
            sb.Append("logs");
            AppendTag(sb, "level", evt.Level.Key);
            AppendTag(sb, "category", evt.Category.Value);
            sb.Append(' ');
            sb.Append("message=\"");
            sb.Append(EscapeFieldValue(evt.Message ?? string.Empty));
            sb.Append('"');

            if (_preserveProperties)
            {
                AppendPreservedFields(sb, evt);
            }

            sb.Append(' ');
            sb.Append(evt.TimeUtc.ToUnixTimeMilliseconds());
            sb.Append('\n');
        }
        return sb.ToString();
    }

    // Append arbitrary properties as line-protocol FIELDS (never tags),
    // type-mapped. The first field ("message") is always present, so each
    // preserved field is comma-prefixed. Reserved keys are skipped; the
    // soft cap bounds the count and surfaces a one-time diagnostic on trip.
    private void AppendPreservedFields(StringBuilder sb, LogEvent evt)
    {
        int written = 0;
        bool capTripped = false;

        foreach (var property in evt.Properties)
        {
            if (ReservedKeys.Contains(property.Name)) continue;

            if (written >= _preserveFieldLimit)
            {
                capTripped = true;
                break;
            }

            sb.Append(',');
            AppendField(sb, property.Name, property.ResolvedValue);
            written++;
        }

        if (capTripped)
        {
            EmitCapDiagnosticOnce(evt.Properties.Count);
        }
    }

    // InfluxDB field-type mapping (D-002.2): string -> "escaped",
    // bool -> true/false, long/int -> 42i (integer field), double/float
    // -> 42.5, other -> stringify to "...". Field keys are escaped the
    // same way tag keys are (space/comma/equals).
    private static void AppendField(StringBuilder sb, string name, object? value)
    {
        sb.Append(EscapeTag(name));
        sb.Append('=');

        switch (value)
        {
            case null:
                sb.Append("\"\"");
                break;
            case string s:
                sb.Append('"').Append(EscapeFieldValue(s)).Append('"');
                break;
            case bool b:
                sb.Append(b ? "true" : "false");
                break;
            case int i:
                sb.Append(i.ToString(CultureInfo.InvariantCulture)).Append('i');
                break;
            case long l:
                sb.Append(l.ToString(CultureInfo.InvariantCulture)).Append('i');
                break;
            case double d:
                sb.Append(d.ToString(CultureInfo.InvariantCulture));
                break;
            case float f:
                sb.Append(f.ToString(CultureInfo.InvariantCulture));
                break;
            default:
                sb.Append('"').Append(EscapeFieldValue(value.ToString() ?? string.Empty)).Append('"');
                break;
        }
    }

    // Fire the cap diagnostic at most once per sink instance. Trace is the
    // standard .NET self-diagnostic channel (AOT-clean, no new dependency);
    // HeraldSinkBase exposes no per-sink diagnostic hook. See the migration
    // report's escalation note on the missing per-sink diagnostic channel.
    private void EmitCapDiagnosticOnce(int totalPropertyCount)
    {
        if (Interlocked.Exchange(ref _capDiagnosticEmitted, 1) != 0) return;

        Trace.TraceWarning(
            "Herald.Sinks.InfluxDB: preserve_field_limit ({0}) tripped — an event carried {1} properties; extra fields beyond the cap were dropped. Raise preserve_field_limit or reduce per-event property cardinality.",
            _preserveFieldLimit, totalPropertyCount);
    }

    private static void AppendTag(StringBuilder sb, string key, string value)
    {
        sb.Append(',');
        sb.Append(EscapeTag(key));
        sb.Append('=');
        sb.Append(EscapeTag(value));
    }

    private static string EscapeTag(string value) =>
        value.Replace(",", "\\,").Replace("=", "\\=").Replace(" ", "\\ ");

    private static string EscapeFieldValue(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
