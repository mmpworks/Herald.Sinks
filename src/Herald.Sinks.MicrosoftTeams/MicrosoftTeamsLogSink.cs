// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using MMP.Herald;
using MMP.Herald.Events;
using MMP.Herald.Services;

namespace Herald.Sinks.MicrosoftTeams;

/// <summary>
/// Sink that posts log events to a Microsoft Teams channel via an
/// incoming webhook. Drop-in for Serilog.Sinks.MicrosoftTeams.Alternative.
/// Uses MessageCard format — simple and compatible with both Office 365
/// Connectors and the new Workflows webhook.
/// </summary>
/// <remarks>
/// <para>
/// Teams webhooks are rate-limited (~1 message/second). Pair this sink
/// with a level filter so only warn+ events fire — a chatty info-level
/// pipeline will trigger 429 responses.
/// </para>
/// <para>
/// Colour mapping: Warn → amber, Error/Critical/Security → red,
/// everything else → blue.
/// </para>
/// </remarks>
public sealed class MicrosoftTeamsLogSink : ILogger, IDisposable
{
    private readonly Uri _webhookUrl;
    private readonly string? _titleOverride;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public MicrosoftTeamsLogSink(
        string webhookUrl,
        string? titleOverride = null,
        HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(webhookUrl);
        _webhookUrl = new Uri(webhookUrl);
        _titleOverride = titleOverride;
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        var body = BuildMessageCard(logEvent);

        using var request = new HttpRequestMessage(HttpMethod.Post, _webhookUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        using var response = _httpClient.Send(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
    }

    private string BuildMessageCard(LogEvent evt)
    {
        var title = _titleOverride ?? $"[{evt.Level.Key}] {evt.Category.Value}";
        var message = string.IsNullOrEmpty(evt.Message) ? evt.MessageTemplate : evt.Message;
        var color = MapColor(evt.Level.Key);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("@type", "MessageCard");
            writer.WriteString("@context", "https://schema.org/extensions");
            writer.WriteString("themeColor", color);
            writer.WriteString("title", title);
            writer.WriteString("text", message);

            writer.WriteStartArray("sections");
            writer.WriteStartObject();
            writer.WriteStartArray("facts");
            WriteFact(writer, "Time", evt.TimeUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            WriteFact(writer, "Level", evt.Level.Key);
            WriteFact(writer, "Category", evt.Category.Value);

            if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value) && value is Exception ex)
            {
                WriteFact(writer, "Exception", ex.GetType().FullName ?? ex.GetType().Name);
            }

            if (evt.Properties is not null && evt.Properties.Count > 0)
            {
                foreach (var prop in evt.Properties)
                {
                    WriteFact(writer, prop.Name, prop.ResolvedValue?.ToString() ?? string.Empty);
                }
            }

            writer.WriteEndArray();  // facts
            writer.WriteEndObject();
            writer.WriteEndArray();  // sections

            writer.WriteEndObject();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteFact(Utf8JsonWriter writer, string name, string value)
    {
        writer.WriteStartObject();
        writer.WriteString("name", name);
        writer.WriteString("value", value);
        writer.WriteEndObject();
    }

    // Hex colours (no '#') for MessageCard themeColor.
    private static string MapColor(string levelKey) => levelKey switch
    {
        "warn" => "D29922",           // amber
        "error" or "critical" or "security" => "D73A49",  // red
        _ => "2188FF",                // blue
    };
}
