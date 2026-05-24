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

namespace Herald.Sinks.PagerDuty;

/// <summary>
/// Sink that posts log events to PagerDuty's Events API v2 for incident
/// creation. Designed for high-severity alerting, not general log
/// forwarding — stack this sink behind a level filter set to <c>error</c>
/// or <c>critical</c> so routine info events don't page the on-call
/// rotation.
/// </summary>
/// <remarks>
/// <para>
/// Wire format: <c>POST https://events.pagerduty.com/v2/enqueue</c>. Body:
/// <code>
/// {
///   "routing_key": "R02...",
///   "event_action": "trigger" | "acknowledge" | "resolve",
///   "dedup_key": "&lt;stable id&gt;",
///   "payload": {
///     "summary": "one-line event summary",
///     "severity": "critical" | "error" | "warning" | "info",
///     "source": "host.example.com",
///     "component": "auth",
///     "group": "production",
///     "class": "exception",
///     "custom_details": { ... arbitrary JSON ... }
///   }
/// }
/// </code>
/// </para>
/// <para>
/// <b>event_action</b> is always <c>trigger</c> in this sink's current
/// shape — Herald emits new incidents but doesn't acknowledge or
/// resolve existing ones (that workflow lives elsewhere in the
/// operator's runbook).
/// </para>
/// <para>
/// <b>dedup_key</b> collapses repeated incidents into a single open
/// alert. The sink derives it from the event's <see cref="LogEventId"/>
/// when set, otherwise from the message template so "sink X timed out"
/// produces one open incident rather than one per retry. Operators who
/// need per-event deduplication pass an explicit dedup-key resolver via
/// the constructor.
/// </para>
/// <para>
/// <b>severity</b> maps Herald levels to PagerDuty's four-value scale.
/// Herald's <c>fatal</c>/<c>critical</c> → <c>critical</c>, <c>error</c>
/// /<c>security</c> → <c>error</c>, <c>warn</c> → <c>warning</c>,
/// everything else → <c>info</c>. Info events shouldn't really be
/// reaching this sink, but the mapping keeps the payload valid.
/// </para>
/// </remarks>
public sealed class PagerDutyLogSink : HeraldSinkBase, IDisposable, INetworkSink
{
    public const string EnqueueEndpoint = "https://events.pagerduty.com/v2/enqueue";

    private readonly Uri _endpoint;
    private readonly string _routingKey;
    private readonly string _source;
    private readonly string? _component;
    private readonly string? _group;
    private readonly Func<LogEvent, string>? _dedupKeyResolver;
    private readonly PagerDutyDedupStrategy _dedupStrategy;
    private readonly IReadOnlyDictionary<string, string>? _customDetailsTemplate;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    /// <summary>
    /// Create a PagerDuty Events API v2 sink.
    /// </summary>
    /// <remarks>
    /// Two new operator-facing knobs land in this ctor:
    /// <list type="bullet">
    ///   <item><paramref name="dedupStrategy"/> picks how the sink
    ///         derives the <c>dedup_key</c>. Defaults to
    ///         <see cref="PagerDutyDedupStrategy.Auto"/> — the same
    ///         event-id → template → message fallback chain the
    ///         sink has always used. Operators who want category- or
    ///         message-only deduplication can pin the strategy here.</item>
    ///   <item><paramref name="customDetailsTemplate"/> merges into the
    ///         outgoing <c>payload.custom_details</c> block before the
    ///         event's own properties land. Common use: a
    ///         <c>runbook_url</c> or <c>team</c> tag that every
    ///         incident from this sink should carry. Event properties
    ///         win on key collision so per-event data is never masked.</item>
    /// </list>
    /// <paramref name="dedupKeyResolver"/> still wins over
    /// <paramref name="dedupStrategy"/> — code-first callers keep full
    /// control.
    /// </remarks>
    public PagerDutyLogSink(
        string routingKey,
        string? source = null,
        string? component = null,
        string? group = null,
        string? endpoint = null,
        Func<LogEvent, string>? dedupKeyResolver = null,
        HttpClient? httpClient = null,
        PagerDutyDedupStrategy dedupStrategy = PagerDutyDedupStrategy.Auto,
        IReadOnlyDictionary<string, string>? customDetailsTemplate = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);

        _routingKey = routingKey;
        _source = string.IsNullOrWhiteSpace(source) ? Environment.MachineName : source;
        _component = Nullify(component);
        _group = Nullify(group);
        _dedupKeyResolver = dedupKeyResolver;
        _dedupStrategy = dedupStrategy;
        _customDetailsTemplate = customDetailsTemplate is null || customDetailsTemplate.Count == 0
            ? null
            : customDetailsTemplate;
        _endpoint = new Uri(string.IsNullOrWhiteSpace(endpoint) ? EnqueueEndpoint : endpoint,
            UriKind.Absolute);
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        var payload = BuildEventPayload(logEvent);
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

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

    private string BuildEventPayload(LogEvent evt)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("routing_key", _routingKey);
            writer.WriteString("event_action", "trigger");
            writer.WriteString("dedup_key", ResolveDedupKey(evt));

            writer.WriteStartObject("payload");
            writer.WriteString("summary", Summarise(evt));
            writer.WriteString("severity", MapSeverity(evt.Level.Key));
            writer.WriteString("source", _source);
            writer.WriteString("timestamp", evt.TimeUtc.ToUniversalTime().ToString("O"));
            writer.WriteString("class", evt.Category.Value);
            if (_component is not null) writer.WriteString("component", _component);
            if (_group is not null) writer.WriteString("group", _group);

            WriteCustomDetails(writer, evt, _customDetailsTemplate);

            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private string ResolveDedupKey(LogEvent evt)
    {
        if (_dedupKeyResolver is not null) return _dedupKeyResolver(evt);

        // Strategy-driven derivation. Auto preserves the original
        // event-id → template → message fallback chain so deployments
        // that never picked a strategy keep their existing dedup
        // behaviour. The other three pin a single shape so an operator
        // running a per-category alert page can guarantee one open
        // incident per category regardless of message text.
        return _dedupStrategy switch
        {
            PagerDutyDedupStrategy.EventId  => evt.EventId is { } eid
                ? $"herald-event-{eid.Id}"
                : $"herald-template-{StableHash(evt.MessageTemplate)}",
            PagerDutyDedupStrategy.Template => $"herald-template-{StableHash(evt.MessageTemplate ?? string.Empty)}",
            PagerDutyDedupStrategy.Category => $"herald-category-{evt.Category.Value}",
            PagerDutyDedupStrategy.Message  => $"herald-message-{StableHash(evt.Message ?? string.Empty)}",
            _ => AutoDedupKey(evt),
        };
    }

    private static string AutoDedupKey(LogEvent evt)
    {
        if (evt.EventId is { } eid) return $"herald-event-{eid.Id}";
        if (!string.IsNullOrWhiteSpace(evt.MessageTemplate))
        {
            return $"herald-template-{StableHash(evt.MessageTemplate)}";
        }
        return $"herald-message-{StableHash(evt.Message)}";
    }

    private static string Summarise(LogEvent evt)
    {
        // PagerDuty truncates summary at 1024 chars; Herald messages
        // rarely hit that, but a defensive cap keeps the payload under
        // control when someone logs a giant error blob.
        var msg = evt.Message;
        if (msg.Length <= 1024) return msg;
        return msg[..1021] + "...";
    }

    private static string MapSeverity(string heraldKey)
    {
        return heraldKey.ToLowerInvariant() switch
        {
            "critical" or "fatal" or "emergency" or "alert" => "critical",
            "error" or "security" => "error",
            "warn" or "warning" => "warning",
            _ => "info",
        };
    }

    private static void WriteCustomDetails(
        Utf8JsonWriter writer,
        LogEvent evt,
        IReadOnlyDictionary<string, string>? template)
    {
        var hasProperties = evt.Properties.Count > 0;
        var hasContext = false;
        foreach (var pair in evt.Context)
        {
            if (pair.Key != LogContextKeys.Exception) { hasContext = true; break; }
        }
        var hasException = evt.Context.ContainsKey(LogContextKeys.Exception);
        var hasTemplate = template is not null && template.Count > 0;

        if (!hasProperties && !hasContext && !hasException && !hasTemplate) return;

        writer.WriteStartObject("custom_details");
        writer.WriteString("messageTemplate", evt.MessageTemplate);

        if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value) && value is Exception ex)
        {
            writer.WriteString("exception", ex.ToString());
            writer.WriteString("exception.type", ex.GetType().FullName ?? ex.GetType().Name);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "messageTemplate", "exception", "exception.type"
        };

        // Event properties + context land first so per-event data
        // claims its keys before the template tries to fill them in.
        // The template only fills gaps the event didn't supply — that
        // matches the ordering operators expect ("runbook URL stays
        // constant unless this event carries a more specific one").
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

        if (template is not null)
        {
            foreach (var pair in template)
            {
                if (string.IsNullOrEmpty(pair.Key)) continue;
                if (seen.Add(pair.Key))
                {
                    writer.WriteString(pair.Key, pair.Value ?? string.Empty);
                }
            }
        }

        writer.WriteEndObject();
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

    private static string StableHash(string value)
    {
        // Non-cryptographic stable hash — FNV-1a 64-bit. Collisions are
        // fine here: we just need one dedup key per distinct template so
        // PagerDuty can fold repeats. Cryptographic strength would cost
        // CPU without buying anything for this use case.
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offsetBasis;
        for (var i = 0; i < value.Length; i++)
        {
            hash ^= value[i];
            hash *= prime;
        }
        return hash.ToString("x", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string? Nullify(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
