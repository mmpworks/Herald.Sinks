// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Sinks.Batching;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.Discord;

/// <summary>
/// Sink that posts log events to a Discord channel via an incoming
/// webhook. One message per event with category prefix and content
/// truncated to Discord's 2000-character message ceiling.
/// </summary>
public sealed class DiscordLogSink : BatchingNetworkSinkBase, IDisposable, INetworkSink
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

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        using var request = BuildRequest(logEvent);
        // True synchronous send — no captured-context dependency, so this is
        // deadlock-safe on a SynchronizationContext-bearing thread.
        using var response = _http.Send(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();
    }

    public async ValueTask LogAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        using var request = BuildRequest(logEvent);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage BuildRequest(LogEvent logEvent)
    {
        var content = new StringContent(BuildBody(logEvent), Encoding.UTF8, "application/json");
        return new HttpRequestMessage(HttpMethod.Post, _webhook) { Content = content };
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }

    private static string BuildBody(LogEvent evt)
    {
        var prefix = evt.Level.Key switch
        {
            "error" or "fatal" or "security" => ":rotating_light:",
            "warning" => ":warning:",
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
