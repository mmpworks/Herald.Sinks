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

namespace Herald.Sinks.Rollbar;

/// <summary>
/// Sink that reports log events to Rollbar via the public Items API.
/// HTTP-only — no Rollbar SDK dependency.
/// </summary>
public sealed class RollbarLogSink : HeraldSinkBase, IDisposable
{
    private const string DefaultEndpoint = "https://api.rollbar.com/api/1/item/";

    private readonly HttpClient _http;
    private readonly string _accessToken;
    private readonly string _environment;
    private readonly Uri _endpoint;
    private readonly bool _ownsHttp;

    public RollbarLogSink(string accessToken, string environment = "production", string? endpoint = null, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);
        _accessToken = accessToken;
        _environment = environment;
        _endpoint = new Uri(endpoint ?? DefaultEndpoint, UriKind.Absolute);
        _ownsHttp = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        var body = BuildBody(logEvent);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = _http.PostAsync(_endpoint, content).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }

    private string BuildBody(LogEvent evt)
    {
        // Rollbar Items API: {"access_token": ..., "data": {"environment": ..., "level": ..., "body": {...}}}
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("access_token", _accessToken);

            writer.WriteStartObject("data");
            writer.WriteString("environment", _environment);
            writer.WriteString("level", MapLevel(evt.Level.Key));
            writer.WriteString("timestamp", evt.TimeUtc.ToUnixTimeSeconds().ToString());
            writer.WriteString("platform", "dotnet");

            writer.WriteStartObject("body");
            writer.WriteStartObject("message");
            writer.WriteString("body", evt.Message ?? string.Empty);
            writer.WriteString("template", evt.MessageTemplate ?? string.Empty);
            writer.WriteString("category", evt.Category.Value);
            writer.WriteEndObject();
            writer.WriteEndObject();

            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string MapLevel(string levelKey) => levelKey switch
    {
        "trace" or "debug" => "debug",
        "info" or "notice" => "info",
        "warn" => "warning",
        "error" => "error",
        "critical" or "security" => "critical",
        _ => "info",
    };
}
