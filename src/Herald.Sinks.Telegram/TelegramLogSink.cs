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

namespace Herald.Sinks.Telegram;

/// <summary>
/// Sink that sends log events to a Telegram chat via the Bot API.
/// One sendMessage per event; truncates content at Telegram's 4000-char
/// safe ceiling (the hard limit is 4096).
/// </summary>
public sealed class TelegramLogSink : ILogger, IDisposable
{
    private const int TelegramMaxLength = 4000;

    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly string _chatId;
    private readonly bool _ownsHttp;

    public TelegramLogSink(string botToken, string chatId, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(botToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(chatId);

        _endpoint = new Uri($"https://api.telegram.org/bot{botToken}/sendMessage", UriKind.Absolute);
        _chatId = chatId;
        _ownsHttp = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public void Log(LogEvent logEvent)
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
        var text = $"[{evt.Level.Key}] {evt.Category.Value}: {evt.Message}";
        if (text.Length > TelegramMaxLength) text = text[..TelegramMaxLength] + "…";

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("chat_id", _chatId);
            writer.WriteString("text", text);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
