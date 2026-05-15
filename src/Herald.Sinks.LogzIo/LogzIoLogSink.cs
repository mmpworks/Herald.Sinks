// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
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

namespace Herald.Sinks.LogzIo;

/// <summary>
/// Sink that ships log events to Logz.io's bulk HTTP listener.
/// Drop-in for Serilog.Sinks.Logz.Io. Token travels in the URL; body
/// is newline-delimited JSON (one event per line) per Logz.io's spec.
/// </summary>
/// <remarks>
/// Default listener endpoint is <c>https://listener.logz.io:8071/</c>;
/// regional listeners (EU, AU) have their own hostnames. Supply the
/// regional URL if your account is not on the US stack.
/// </remarks>
public sealed class LogzIoLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable, INetworkSink
{
    private readonly Uri _endpoint;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public LogzIoLogSink(
        string accountToken,
        string type = "herald",
        string listenerUrl = "https://listener.logz.io:8071/",
        HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(listenerUrl);

        _endpoint = new Uri($"{listenerUrl.TrimEnd('/')}/?token={Uri.EscapeDataString(accountToken)}&type={Uri.EscapeDataString(type)}");
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        LogBatch(new[] { logEvent });
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        var body = BuildBody(events);

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
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

    private static string BuildBody(IReadOnlyList<LogEvent> events)
    {
        // NDJSON: one JSON object per line, no enclosing array. Logz.io
        // streams these via the bulk listener.
        var sb = new StringBuilder();
        foreach (var evt in events)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("@timestamp", evt.TimeUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
                writer.WriteString("level", evt.Level.Key);
                writer.WriteString("category", evt.Category.Value);
                writer.WriteString("message", evt.Message ?? string.Empty);
                writer.WriteString("template", evt.MessageTemplate ?? string.Empty);

                if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value) && value is Exception ex)
                {
                    writer.WriteString("exception", ex.ToString());
                    writer.WriteString("exception.type", ex.GetType().FullName ?? ex.GetType().Name);
                }

                if (evt.Properties is not null && evt.Properties.Count > 0)
                {
                    foreach (var prop in evt.Properties)
                    {
                        writer.WriteString(prop.Name, prop.ResolvedValue?.ToString());
                    }
                }

                writer.WriteEndObject();
                writer.Flush();
            }
            sb.Append(Encoding.UTF8.GetString(stream.ToArray()));
            sb.Append('\n');
        }
        return sb.ToString();
    }
}
