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

namespace Herald.Sinks.SumoLogic;

/// <summary>
/// Sink that posts Herald log events to a Sumo Logic HTTP source
/// endpoint. Drop-in for SumoLogic.Logging.Serilog. Pure HTTP — the
/// source URL itself encodes the collector and auth.
/// </summary>
/// <remarks>
/// Sumo Logic's HTTP source URL is single-use authentication: the
/// path token is the credential. Optional <c>X-Sumo-Name</c>,
/// <c>X-Sumo-Category</c>, and <c>X-Sumo-Host</c> headers tag events
/// for searchability inside Sumo.
/// </remarks>
public sealed class SumoLogicLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable
{
    private readonly Uri _sourceUrl;
    private readonly string? _sourceCategory;
    private readonly string? _sourceName;
    private readonly string? _sourceHost;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public SumoLogicLogSink(
        string sourceUrl,
        string? sourceCategory = null,
        string? sourceName = null,
        string? sourceHost = null,
        HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUrl);
        _sourceUrl = new Uri(sourceUrl);
        _sourceCategory = sourceCategory;
        _sourceName = sourceName;
        _sourceHost = sourceHost;
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

        var sb = new StringBuilder();
        foreach (var evt in events)
        {
            sb.Append(BuildJson(evt)).Append('\n');
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _sourceUrl)
        {
            Content = new StringContent(sb.ToString(), Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrEmpty(_sourceCategory)) request.Headers.TryAddWithoutValidation("X-Sumo-Category", _sourceCategory);
        if (!string.IsNullOrEmpty(_sourceName)) request.Headers.TryAddWithoutValidation("X-Sumo-Name", _sourceName);
        if (!string.IsNullOrEmpty(_sourceHost)) request.Headers.TryAddWithoutValidation("X-Sumo-Host", _sourceHost);

        using var response = _httpClient.Send(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
    }

    private static string BuildJson(LogEvent evt)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("timestamp", evt.TimeUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("level", evt.Level.Key);
            writer.WriteString("category", evt.Category.Value);
            writer.WriteString("message", evt.Message ?? string.Empty);
            writer.WriteString("template", evt.MessageTemplate ?? string.Empty);

            if (evt.Context.TryGetValue(LogContextKeys.Exception, out var v) && v is Exception ex)
                writer.WriteString("exception", ex.ToString());

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
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
