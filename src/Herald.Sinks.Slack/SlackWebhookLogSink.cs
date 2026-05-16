// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Net.Http;
using System.Text;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Pipeline;

namespace Herald.Sinks.Slack;

/// <summary>
/// Sends log events to a Slack webhook URL as formatted messages.
/// Maps log levels to Slack emoji and includes structured properties.
///
/// Best used for high-severity alerts (Error, Critical) rather than all events.
/// Combine with a LevelFilter or predicate to limit volume.
/// </summary>
public sealed class SlackWebhookLogSink : HeraldSinkBase, IDisposable, INetworkSink
{
    internal static readonly HeraldEdition MinEdition = HeraldEdition.Community;

    private readonly string _webhookUrl;
    private readonly string _channel;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;
    private readonly ILogLevelRegistry _levelRegistry;

    public SlackWebhookLogSink(
        string webhookUrl,
        ILogLevelRegistry levelRegistry,
        string? channel = null,
        HttpClient? httpClient = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(webhookUrl);
        _webhookUrl = webhookUrl;
        _levelRegistry = levelRegistry ?? throw new ArgumentNullException(nameof(levelRegistry));
        _channel = channel ?? "#alerts";
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _ownsClient = httpClient is null;
    }

    public override void Log(LogEvent logEvent) {
        ArgumentNullException.ThrowIfNull(logEvent);

        var emoji = MapLevelToEmoji(logEvent.Level);
        var registeredLevel = _levelRegistry.GetRegisteredLevel(logEvent.Level);
        var text = $"{emoji} *[{registeredLevel.Level.DisplayName}]* {logEvent.Category.Value}: {EscapeSlack(logEvent.Message)}";

        // Two escape passes on purpose. JSON escaping protects the Slack
        // webhook's JSON body from quote / newline / backslash injection.
        // Slack escaping (applied to Message before JSON-escaping here via
        // the `text` construction above) converts markup that Slack would
        // otherwise interpret — ampersand, less-than, greater-than — into
        // HTML entities so a message like "2 < 3" renders literally in
        // the channel instead of being dropped as a malformed tag.
        var payload = $"{{\"channel\":\"{EscapeJson(_channel)}\",\"text\":\"{EscapeJson(text)}\"}}";
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, _webhookUrl) { Content = content };
        using var response = _httpClient.Send(request);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }

    private static string MapLevelToEmoji(LogLevel level) {
        return level.Key.ToLowerInvariant() switch
        {
            "trace" or "debug" => ":mag:",
            "info" or "notice" => ":information_source:",
            "success" => ":white_check_mark:",
            "warn" => ":warning:",
            "error" => ":x:",
            "critical" or "fatal" => ":fire:",
            "security" => ":lock:",
            _ => ":speech_balloon:"
        };
    }

    private static string EscapeJson(string value) {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal);
    }

    private static string EscapeSlack(string value) {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }
}
