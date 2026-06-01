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
using MMP.Herald.Sinks.Batching;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;

namespace Herald.Sinks.Sentry;

/// <summary>
/// Sink that posts log events to Sentry's store endpoint. Sentry is an
/// error-tracking platform; this sink lets Herald forward warnings and
/// errors into the same dashboard the dev team already uses to triage
/// exceptions. Lower-severity events pass through too when the caller
/// configures the sink to accept them, but the typical deployment gates
/// Sentry on Warn or above.
/// </summary>
/// <remarks>
/// <para>
/// Sentry's DSN shape is
/// <c>https://{publicKey}@o{orgId}.ingest.sentry.io/{projectId}</c>. The
/// sink parses the DSN at construction: public key flows into the
/// <c>X-Sentry-Auth</c> header, host and project id compose the store
/// URL <c>https://o{org}.ingest.sentry.io/api/{project}/store/</c>.
/// </para>
/// <para>
/// Wire format: one JSON event per POST (Sentry batching uses a
/// separate envelope endpoint; this sink uses the simpler store
/// endpoint). The event carries <c>message</c>, <c>level</c>,
/// <c>platform</c>, <c>logger</c>, <c>timestamp</c>, optional
/// <c>exception</c>, and <c>extra</c> for free-form properties.
/// </para>
/// <para>
/// Level mapping targets Sentry's known severity strings —
/// <c>fatal</c>, <c>error</c>, <c>warning</c>, <c>info</c>, <c>debug</c>.
/// Herald's <c>critical</c> / <c>fatal</c> both map to Sentry's
/// <c>fatal</c>; unknowns fall to <c>info</c>.
/// </para>
/// <para>
/// Self-hosted Sentry installations use the same DSN shape with a
/// different host — pass the full DSN and everything else works as-is.
/// </para>
/// </remarks>
public sealed class SentryLogSink : BatchingNetworkSinkBase, IDisposable, INetworkSink
{
    private readonly Uri _storeEndpoint;
    private readonly string _authHeader;
    private readonly string _logger;
    private readonly string _platform;
    private readonly IReadOnlyDictionary<string, string>? _tags;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public SentryLogSink(
        string dsn,
        string? logger = null,
        string? platform = null,
        IReadOnlyDictionary<string, string>? tags = null,
        HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dsn);

        var parsed = SentryDsn.Parse(dsn);
        _storeEndpoint = parsed.StoreEndpoint;
        _authHeader = parsed.BuildAuthHeader();
        _logger = string.IsNullOrWhiteSpace(logger) ? "herald" : logger;
        _platform = string.IsNullOrWhiteSpace(platform) ? "csharp" : platform;
        _tags = NormaliseTags(tags);
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        var payload = BuildEvent(logEvent);
        using var request = new HttpRequestMessage(HttpMethod.Post, _storeEndpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Sentry-Auth", _authHeader);

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

    private string BuildEvent(LogEvent evt)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();

            writer.WriteString("event_id", Guid.NewGuid().ToString("N"));
            writer.WriteString("timestamp", evt.TimeUtc.ToUniversalTime().ToString("O"));
            writer.WriteString("logger", _logger);
            writer.WriteString("platform", _platform);
            writer.WriteString("level", MapLevel(evt.Level.Key));

            writer.WriteStartObject("message");
            writer.WriteString("message", evt.Message);
            if (!string.Equals(evt.Message, evt.MessageTemplate, StringComparison.Ordinal))
            {
                writer.WriteString("formatted", evt.Message);
                writer.WriteString("params", evt.MessageTemplate);
            }
            writer.WriteEndObject();

            WriteTags(writer, evt);
            WriteException(writer, evt);
            WriteExtra(writer, evt);

            writer.WriteEndObject();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private void WriteTags(Utf8JsonWriter writer, LogEvent evt)
    {
        // Sentry's "tags" are low-cardinality labels that show up as
        // filter chips in the UI. Static tags from construction plus
        // the event's category — properties do NOT become tags because
        // property cardinality is unbounded.
        writer.WriteStartObject("tags");
        writer.WriteString("category", evt.Category.Value);
        if (_tags is not null)
        {
            foreach (var pair in _tags)
            {
                writer.WriteString(pair.Key, pair.Value);
            }
        }
        writer.WriteEndObject();
    }

    private static void WriteException(Utf8JsonWriter writer, LogEvent evt)
    {
        if (!evt.Context.TryGetValue(LogContextKeys.Exception, out var value)
            || value is not Exception ex)
        {
            return;
        }

        // Sentry's exception shape: { values: [{ type, value, stacktrace }] }.
        // We emit a single-value list; Sentry joins multiple exceptions via
        // AggregateException / inner-exception walking which would be nice
        // to surface but can come in a second pass if operators ask.
        writer.WriteStartObject("exception");
        writer.WriteStartArray("values");
        writer.WriteStartObject();
        writer.WriteString("type", ex.GetType().FullName ?? ex.GetType().Name);
        writer.WriteString("value", ex.Message);
        if (!string.IsNullOrEmpty(ex.StackTrace))
        {
            writer.WriteString("stacktrace", ex.StackTrace);
        }
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteExtra(Utf8JsonWriter writer, LogEvent evt)
    {
        // "extra" is Sentry's free-form property bag — anything the UI
        // doesn't have a built-in spot for. Properties and non-exception
        // context entries land here so they remain queryable without
        // inflating tag cardinality.
        var hasProperties = evt.Properties.Count > 0;
        var hasContext = HasNonExceptionContext(evt);
        if (!hasProperties && !hasContext) return;

        writer.WriteStartObject("extra");
        var seen = new HashSet<string>(StringComparer.Ordinal);
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

    private static bool HasNonExceptionContext(LogEvent evt)
    {
        foreach (var pair in evt.Context)
        {
            if (pair.Key != LogContextKeys.Exception) return true;
        }
        return false;
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

    private static string MapLevel(string heraldKey)
    {
        // Sentry accepts debug / info / warning / error / fatal.
        return heraldKey.ToLowerInvariant() switch
        {
            "verbose" or "debug" => "debug",
            "information" or "notice" or "success" or "metric" => "info",
            "warning" => "warning",
            "error" or "security" => "error",
            "fatal" or "alert" or "emergency" => "fatal",
            _ => "info",
        };
    }

    private static IReadOnlyDictionary<string, string>? NormaliseTags(
        IReadOnlyDictionary<string, string>? tags)
    {
        if (tags is null || tags.Count == 0) return null;
        var normalised = new Dictionary<string, string>(tags.Count, StringComparer.Ordinal);
        foreach (var pair in tags)
        {
            if (string.IsNullOrWhiteSpace(pair.Key)) continue;
            normalised[pair.Key] = pair.Value ?? string.Empty;
        }
        return normalised.Count == 0 ? null : normalised;
    }
}

/// <summary>
/// Parsed Sentry DSN. Sentry's DSN format packs the public key, host,
/// and project id into one URL; splitting it here keeps the sink
/// constructor free of URL gymnastics.
/// </summary>
internal readonly struct SentryDsn
{
    public Uri StoreEndpoint { get; }
    public string PublicKey { get; }

    private SentryDsn(Uri storeEndpoint, string publicKey)
    {
        StoreEndpoint = storeEndpoint;
        PublicKey = publicKey;
    }

    public static SentryDsn Parse(string dsn)
    {
        // Shape: https://{publicKey}@o{orgId}.ingest.sentry.io/{projectId}
        // (self-hosted installs replace the host but keep the userinfo +
        // trailing project-id path component).
        var uri = new Uri(dsn, UriKind.Absolute);

        var userInfo = uri.UserInfo;
        if (string.IsNullOrWhiteSpace(userInfo))
        {
            throw new ArgumentException(
                "Sentry DSN must include the public key as user-info (scheme://KEY@host/project).",
                nameof(dsn));
        }
        // When the DSN carries both public and secret keys separated by a
        // colon, the secret half is deprecated and ignored by Sentry
        // itself; drop it here so we never leak it in a header.
        var publicKey = userInfo.Contains(':')
            ? userInfo[..userInfo.IndexOf(':')]
            : userInfo;

        var projectId = uri.AbsolutePath.Trim('/');
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new ArgumentException(
                "Sentry DSN path must include the numeric project id.",
                nameof(dsn));
        }

        var store = new Uri($"{uri.Scheme}://{uri.Host}:{uri.Port}/api/{projectId}/store/",
            UriKind.Absolute);
        return new SentryDsn(store, publicKey);
    }

    public string BuildAuthHeader()
    {
        // Sentry wants a specific comma-separated string with version,
        // timestamp, and key. sentry_version=7 is the stable wire version
        // the store endpoint accepts; bumping it requires a server-side
        // contract change we don't own.
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return $"Sentry sentry_version=7, sentry_client=herald/1.0, " +
               $"sentry_timestamp={ts}, sentry_key={PublicKey}";
    }
}
