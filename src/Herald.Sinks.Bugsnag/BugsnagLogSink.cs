// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.Bugsnag;

/// <summary>
/// Sink that reports log events to Bugsnag via the public notify API.
/// Drop-in for Serilog.Sinks.Bugsnag. Uses plain HTTP — no Bugsnag.NET
/// SDK dependency.
/// </summary>
/// <remarks>
/// <para>
/// <b>What gets reported.</b> Every event becomes a Bugsnag <c>event</c>
/// with severity mapped from the level key. Events at <c>warn</c> and
/// above promote naturally; lower-severity logs still post but are
/// rate-limited by Bugsnag itself.
/// </para>
/// </remarks>
public sealed class BugsnagLogSink : HeraldSinkBase, IDisposable, INetworkSink
{
    private const string DefaultEndpoint = "https://notify.bugsnag.com/";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly Uri _endpoint;
    private readonly string? _releaseStage;
    private readonly bool _ownsHttp;

    /// <summary>
    /// Create a Bugsnag sink. <paramref name="releaseStage"/> populates
    /// the Bugsnag <c>app.releaseStage</c> field, which drives the
    /// Bugsnag dashboard's environment filter (production / staging /
    /// development). Leaving it null skips the field entirely — Bugsnag
    /// then treats events as belonging to the project's default stage.
    /// </summary>
    public BugsnagLogSink(
        string apiKey,
        string? endpoint = null,
        HttpClient? httpClient = null,
        string? releaseStage = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        _apiKey = apiKey;
        _endpoint = new Uri(endpoint ?? DefaultEndpoint, UriKind.Absolute);
        _releaseStage = string.IsNullOrWhiteSpace(releaseStage) ? null : releaseStage;
        _ownsHttp = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        var body = BuildBody(logEvent);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        content.Headers.Add("Bugsnag-Api-Key", _apiKey);
        content.Headers.Add("Bugsnag-Payload-Version", "5");
        using var response = _http.PostAsync(_endpoint, content).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }

    private string BuildBody(LogEvent evt)
    {
        // Bugsnag payload v5: {"apiKey":...,"notifier":{...},"events":[{...}]}
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("apiKey", _apiKey);

            writer.WriteStartObject("notifier");
            writer.WriteString("name", "Herald.Sinks.Bugsnag");
            writer.WriteString("version", "1.0.0");
            writer.WriteString("url", "https://github.com/mmpworks/Herald.Sinks");
            writer.WriteEndObject();

            writer.WriteStartArray("events");
            writer.WriteStartObject();
            writer.WriteString("severity", MapSeverity(evt.Level.Key));

            // Bugsnag's payload v5 carries releaseStage under
            // events[].app.releaseStage. The dashboard filters every
            // saved view on this field — events that omit it land in
            // the project's "default stage" bucket and are easy to
            // miss. Emitting only when the operator set the field
            // keeps the payload truthful instead of defaulting to a
            // string like "unknown" that would mask the gap.
            if (_releaseStage is not null)
            {
                writer.WriteStartObject("app");
                writer.WriteString("releaseStage", _releaseStage);
                writer.WriteEndObject();
            }

            writer.WriteStartArray("exceptions");
            writer.WriteStartObject();
            writer.WriteString("errorClass", evt.Category.Value);
            writer.WriteString("message", evt.Message ?? string.Empty);
            writer.WriteEndObject();
            writer.WriteEndArray();

            writer.WriteStartObject("metaData");
            writer.WriteStartObject("herald");
            writer.WriteString("level", evt.Level.Key);
            writer.WriteString("template", evt.MessageTemplate ?? string.Empty);
            writer.WriteString("time_utc", evt.TimeUtc);
            writer.WriteEndObject();
            writer.WriteEndObject();

            writer.WriteEndObject();
            writer.WriteEndArray();

            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string MapSeverity(string levelKey) => levelKey switch
    {
        "error" or "critical" or "security" => "error",
        "warn" => "warning",
        _ => "info",
    };
}
