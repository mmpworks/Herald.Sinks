// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
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
/// The document shape an <see cref="ElasticsearchLogSink"/> emits.
/// </summary>
public enum ElasticsearchSchema
{
    /// <summary>
    /// The current Herald-native shape (default — no existing consumer
    /// breaks on a sink-package upgrade).
    /// </summary>
    Native = 0,

    /// <summary>
    /// Strict Elastic Common Schema (ECS) document body with dotted
    /// reserved field keys (<c>log.level</c>, <c>service.name</c>, …).
    /// Opt in when the cluster runs an ECS index template.
    /// </summary>
    Ecs = 1,
}

/// <summary>
/// Sends log events to Elasticsearch as JSON documents via the Bulk API.
/// Uses index naming convention: {indexPrefix}-{yyyy.MM.dd} for time-based indices.
///
/// Supports both single-event and batch modes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Schema (default native).</b> The document body defaults to the
/// Herald-native shape so a sink-package upgrade changes nothing for an
/// existing consumer with a pinned native index template. Set
/// <c>schema=ecs</c> to emit a strict ECS document body (dotted reserved
/// field keys, <c>log.level</c> lowercased, <c>error.*</c> for exceptions);
/// do that deliberately when you have an ECS index template ready, on a new
/// index alias.
/// </para>
/// </remarks>
public sealed class ElasticsearchLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable, INetworkSink
{
    internal static readonly HeraldEdition MinEdition = HeraldEdition.Community;

    /// <summary>Default ECS version emitted in <c>ecs.version</c> (ECS mode).</summary>
    public const string DefaultEcsVersion = "8.11.0";

    /// <summary>Default <c>event.dataset</c> value (ECS mode).</summary>
    public const string DefaultEventDataset = "app";

    private readonly string _baseUrl;
    private readonly string _indexPrefix;
    private readonly ILogLevelRegistry _levelRegistry;
    private readonly HttpClient _httpClient;
    private readonly string? _authHeaderScheme;
    private readonly string? _authHeaderValue;
    private readonly bool _ownsClient;
    private readonly ElasticsearchSchema _schema;
    private readonly string _ecsVersion;
    private readonly string _eventDataset;

    /// <summary>
    /// Create an Elasticsearch sink. Two auth modes are supported:
    /// HTTP Basic (supply <paramref name="username"/> + <paramref name="password"/>)
    /// or API key (supply <paramref name="apiKey"/>). When both sets are
    /// present, the API key wins — operators typically rotate one or
    /// the other, not both, and the precedence keeps the rotation
    /// predictable. Leaving both unset yields an unauthenticated
    /// client, which is the prior behaviour.
    /// <para>
    /// <paramref name="schema"/> selects the document body: <see cref="ElasticsearchSchema.Native"/>
    /// (default) keeps today's Herald-native shape; <see cref="ElasticsearchSchema.Ecs"/>
    /// emits a strict ECS body. <paramref name="ecsVersion"/> and
    /// <paramref name="eventDataset"/> are used only in ECS mode.
    /// </para>
    /// </summary>
    public ElasticsearchLogSink(
        string baseUrl,
        ILogLevelRegistry levelRegistry,
        string indexPrefix = "herald-logs",
        HttpClient? httpClient = null,
        string? username = null,
        string? password = null,
        string? apiKey = null,
        ElasticsearchSchema schema = ElasticsearchSchema.Native,
        string? ecsVersion = null,
        string? eventDataset = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        if (!System.Text.RegularExpressions.Regex.IsMatch(indexPrefix, @"^[a-z0-9][a-z0-9_\-\.]{0,127}$"))
            throw new ArgumentException(
                "Index prefix must be 1-128 lowercase alphanumeric characters, hyphens, underscores, or dots.", nameof(indexPrefix));
        _baseUrl = baseUrl.TrimEnd('/');
        _levelRegistry = levelRegistry ?? throw new ArgumentNullException(nameof(levelRegistry));
        _indexPrefix = indexPrefix;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _ownsClient = httpClient is null;
        _schema = schema;
        _ecsVersion = string.IsNullOrWhiteSpace(ecsVersion) ? DefaultEcsVersion : ecsVersion;
        _eventDataset = string.IsNullOrWhiteSpace(eventDataset) ? DefaultEventDataset : eventDataset;

        // Auth resolution: API key beats Basic — the comment on the
        // ctor explains why. Both null leaves the request unsigned.
        (_authHeaderScheme, _authHeaderValue) = ResolveAuth(username, password, apiKey);
    }

    private static (string? Scheme, string? Value) ResolveAuth(
        string? username, string? password, string? apiKey)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return ("ApiKey", apiKey);
        }
        if (!string.IsNullOrWhiteSpace(username) && password is not null)
        {
            var raw = $"{username}:{password}";
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
            return ("Basic", encoded);
        }
        return (null, null);
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
        if (_authHeaderScheme is not null && _authHeaderValue is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                _authHeaderScheme, _authHeaderValue);
        }
        using var response = _httpClient.Send(request);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() {
        if (_ownsClient) _httpClient.Dispose();
    }

    // Single top-of-method branch on the configured schema — the only
    // added control flow. Native is byte-for-byte the prior path; ECS is
    // the strict Elastic Common Schema body.
    private void WriteDocument(Utf8JsonWriter writer, LogEvent logEvent) {
        if (_schema == ElasticsearchSchema.Ecs) {
            WriteEcsDocument(writer, logEvent);
        } else {
            WriteNativeDocument(writer, logEvent);
        }
    }

    // Shape matches Elasticsearch's common Ecs-adjacent layout: @timestamp
    // at the root, severity on its own field, then two nested objects
    // (properties, context) so Elasticsearch's indexing rules treat user
    // property names and the event's ambient context as separate
    // namespaces. Collocating them would let a user property named
    // "exception" collide with a real exception in context — keeping
    // them nested removes the ambiguity.
    private void WriteNativeDocument(Utf8JsonWriter writer, LogEvent logEvent) {
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
                WriteValue(writer, prop.Name, prop.ResolvedValue);
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

    // Strict ECS document body. Dotted reserved keys (log.level,
    // service.name, …), a seen set so a property promoted to a reserved
    // field is not re-emitted, error.* for exceptions, and WriteValue
    // type-dispatch for every other value. The body is split into three
    // helpers so each reads at one level of abstraction:
    //   - WriteEcsReservedFields: the always-present + property-projected reserved set
    //   - WriteEcsException:      the Context exception -> error.* triple
    //   - WritePromotedProperties: arbitrary properties + non-exception context, flat
    private void WriteEcsDocument(Utf8JsonWriter writer, LogEvent logEvent) {
        var registeredLevel = _levelRegistry.GetRegisteredLevel(logEvent.Level);

        // The seen set marks every key already written as a reserved/dotted
        // field so WritePromotedProperties does not duplicate it — the same
        // pattern the Datadog sink uses.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        writer.WriteStartObject();
        WriteEcsReservedFields(writer, logEvent, registeredLevel.Level, seen);
        WriteEcsException(writer, logEvent);
        WritePromotedProperties(writer, logEvent, seen);
        writer.WriteEndObject();
    }

    // The ECS reserved set: always-present fields plus the property
    // projections (service.name / source.ip / user.id) that ECS sources
    // from event properties — mirroring how the Datadog sink projects a
    // UserId property into usr.id. A projected property name is added to
    // `seen` so it is not re-emitted as a free-form dotted field. Reserved
    // fields whose source property is absent are simply omitted (no invented
    // defaults — the sink has no service-name config).
    private void WriteEcsReservedFields(
        Utf8JsonWriter writer, LogEvent logEvent, LogLevel level, HashSet<string> seen) {
        // KEEP full O precision — ES accepts it; the answer key's ms-Z is
        // illustrative, not a ceiling (ADR D-001.6).
        writer.WriteString("@timestamp", logEvent.TimeUtc.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteString("log.level", level.Key.ToLowerInvariant());
        writer.WriteString("message", logEvent.Message);
        writer.WriteString("ecs.version", _ecsVersion);

        WriteProjectedString(writer, logEvent, "service.name", seen, "service.name", "ServiceName");
        WriteProjectedString(writer, logEvent, "source.ip", seen, "source.ip", "SourceIp", "IP");
        WriteProjectedString(writer, logEvent, "user.id", seen, "user.id", "UserId");

        writer.WriteString("event.dataset", _eventDataset);
        writer.WriteString("log.logger", logEvent.Category.Value);
        // Custom non-ECS field so the template survives the projection.
        writer.WriteString("message_template", logEvent.MessageTemplate);
    }

    // Project the first present property (by the given candidate names) onto
    // an ECS reserved dotted field, stringified (ECS user.id is a string;
    // service.name / source.ip are strings too). Marks the matched property
    // name as seen. Omits the field entirely when no candidate is present.
    private static void WriteProjectedString(
        Utf8JsonWriter writer, LogEvent logEvent, string ecsField,
        HashSet<string> seen, params string[] candidateNames) {
        foreach (var property in logEvent.Properties) {
            foreach (var candidate in candidateNames) {
                if (!string.Equals(property.Name, candidate, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                writer.WriteString(ecsField, property.ResolvedValue?.ToString() ?? "");
                seen.Add(property.Name);
                return;
            }
        }
    }

    // ECS error.* set for a Context exception — mirrors the Datadog sink's
    // error.* triple, replacing the native context.exception sub-object.
    private static void WriteEcsException(Utf8JsonWriter writer, LogEvent logEvent) {
        foreach (var pair in logEvent.Context) {
            if (pair.Value is Exception ex) {
                writer.WriteString("error.message", ex.Message);
                writer.WriteString("error.type", ex.GetType().FullName ?? ex.GetType().Name);
                writer.WriteString("error.stack_trace", ex.StackTrace ?? "");
                return;
            }
        }
    }

    // Arbitrary properties and non-exception context values land as flat
    // top-level dotted fields, type-preserved via WriteValue (fixes the F1
    // context-stringify bug). The seen set excludes anything already promoted
    // to a reserved ECS field or written as an error.* key.
    private static void WritePromotedProperties(
        Utf8JsonWriter writer, LogEvent logEvent, HashSet<string> seen) {
        seen.Add("error.message");
        seen.Add("error.type");
        seen.Add("error.stack_trace");

        foreach (var property in logEvent.Properties) {
            if (seen.Add(property.Name)) {
                WriteValue(writer, property.Name, property.ResolvedValue);
            }
        }

        foreach (var pair in logEvent.Context) {
            if (pair.Value is Exception) continue;
            if (seen.Add(pair.Key)) {
                WriteValue(writer, pair.Key, pair.Value);
            }
        }
    }

    // Type-dispatch the same way the Datadog/Splunk/Loki sinks do: a long
    // stays a JSON number, a bool stays a bool. The prior ToString() path
    // flattened every property to a string (42L -> "42"), losing the typed
    // query surface Elasticsearch's numeric mappings key off (F1 defect).
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
}
