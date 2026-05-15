// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;

namespace Herald.Sinks.OpenSearch;

/// <summary>
/// Sink that indexes Herald log events into an OpenSearch cluster via
/// the <c>_bulk</c> HTTP API. Drop-in for Serilog.Sinks.OpenSearch.
/// Pure HTTP — no OpenSearch SDK dependency keeps the transitive
/// footprint small.
/// </summary>
/// <remarks>
/// <para>
/// <b>Index rotation.</b> The index name accepts a date token
/// (<c>{0:yyyy-MM-dd}</c>) so rolling daily indices — the common
/// pattern for time-series log data — works with no extra config.
/// Default: <c>herald-logs-{0:yyyy-MM-dd}</c>.
/// </para>
/// <para>
/// <b>Auth.</b> Optional basic-auth via username/password. For AWS
/// OpenSearch Service (formerly AWS Elasticsearch Service), supply a
/// SigV4-signed HttpClient through the code-first ctor — the sink does
/// not embed AWS SigV4 logic.
/// </para>
/// </remarks>
public sealed class OpenSearchLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable
{
    private readonly Uri _bulkEndpoint;
    private readonly string _indexNameTemplate;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public OpenSearchLogSink(
        string endpoint,
        string indexNameTemplate = "herald-logs-{0:yyyy-MM-dd}",
        string? username = null,
        string? password = null,
        HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(indexNameTemplate);

        _bulkEndpoint = new Uri(endpoint.TrimEnd('/') + "/_bulk");
        _indexNameTemplate = indexNameTemplate;
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        if (username is not null && password is not null)
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);
        }
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

        var body = BuildBulkBody(events);

        using var request = new HttpRequestMessage(HttpMethod.Post, _bulkEndpoint)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/x-ndjson"),
        };

        using var response = _httpClient.Send(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
    }

    private string BuildBulkBody(IReadOnlyList<LogEvent> events)
    {
        // _bulk NDJSON: alternating action + document lines, each line
        // ending in \n (including the final one).
        var sb = new StringBuilder();
        foreach (var evt in events)
        {
            var index = string.Format(CultureInfo.InvariantCulture, _indexNameTemplate, evt.TimeUtc.UtcDateTime);
            sb.Append("{\"index\":{\"_index\":\"").Append(index).Append("\"}}\n");
            sb.Append(BuildDocument(evt)).Append('\n');
        }
        return sb.ToString();
    }

    private static string BuildDocument(LogEvent evt)
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
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
