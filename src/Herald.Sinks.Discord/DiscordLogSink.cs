// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using MMP.Herald;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.Discord;

/// <summary>
/// Sink that posts log events to a Discord channel via an incoming
/// webhook. One message per event with category prefix and content
/// truncated to Discord's 2000-character message ceiling.
/// </summary>
public sealed class DiscordLogSink : ILogger, IDisposable
{
    private const int DiscordMaxLength = 1900; // leave room for prefix

    private readonly HttpClient _http;
    private readonly Uri _webhook;
    private readonly bool _ownsHttp;

    public DiscordLogSink(string webhookUrl, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(webhookUrl);
        _webhook = new Uri(webhookUrl, UriKind.Absolute);
        _ownsHttp = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        var body = BuildBody(logEvent);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = _http.PostAsync(_webhook, content).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }

    private static string BuildBody(LogEvent evt)
    {
        var prefix = evt.Level.Key switch
        {
            "error" or "critical" or "security" => ":rotating_light:",
            "warn" => ":warning:",
            _ => ":speech_balloon:",
        };
        var message = $"{prefix} **[{evt.Level.Key}]** `{evt.Category.Value}` — {evt.Message}";
        if (message.Length > DiscordMaxLength) message = message[..DiscordMaxLength] + "…";

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("content", message);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
