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
using MMP.Herald.Sinks;
using MMP.Herald.Events;
using MMP.Herald.Services;

namespace Herald.Sinks.Raygun;

/// <summary>
/// Sink that forwards Herald log events to the Raygun crash-reporting
/// API. Drop-in for Serilog.Sinks.Raygun. Pure HTTP — no Raygun SDK
/// dependency.
/// </summary>
/// <remarks>
/// <para>
/// Raygun is primarily a crash / error tracker. Pair this sink with a
/// level filter (warn+ or error+) — sending info-level chatter wastes
/// quota. The sink emits one Raygun entry per event with
/// <c>error.message</c> = log message and <c>customData</c> = property
/// bag.
/// </para>
/// </remarks>
public sealed class RaygunLogSink : HeraldSinkBase, IDisposable, INetworkSink
{
    private static readonly Uri Endpoint = new("https://api.raygun.com/entries");

    private readonly string _apiKey;
    private readonly string _appVersion;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public RaygunLogSink(string apiKey, string? appVersion = null, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        _apiKey = apiKey;
        _appVersion = appVersion ?? "1.0.0";
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        var body = BuildEntry(logEvent);

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("X-ApiKey", _apiKey);

        using var response = _httpClient.Send(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
    }

    private string BuildEntry(LogEvent evt)
    {
        var ex = evt.Context.TryGetValue(LogContextKeys.Exception, out var v) ? v as Exception : null;

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("occurredOn", evt.TimeUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));

            writer.WriteStartObject("details");
            writer.WriteString("machineName", Environment.MachineName);
            writer.WriteString("version", _appVersion);

            writer.WriteStartObject("error");
            writer.WriteString("message", evt.Message ?? string.Empty);
            writer.WriteString("className", ex?.GetType().FullName ?? evt.Category.Value);
            writer.WriteString("stackTrace", ex?.StackTrace ?? string.Empty);
            writer.WriteEndObject();

            // Custom data carries Herald properties + level/category for query.
            writer.WriteStartObject("userCustomData");
            writer.WriteString("level", evt.Level.Key);
            writer.WriteString("category", evt.Category.Value);
            writer.WriteString("template", evt.MessageTemplate ?? string.Empty);

            if (evt.Properties is not null && evt.Properties.Count > 0)
            {
                foreach (var prop in evt.Properties)
                {
                    writer.WriteString(prop.Name, prop.ResolvedValue?.ToString());
                }
            }
            writer.WriteEndObject();

            writer.WriteStartArray("tags");
            writer.WriteStringValue(evt.Level.Key);
            writer.WriteStringValue(evt.Category.Value);
            writer.WriteEndArray();

            writer.WriteEndObject();  // details
            writer.WriteEndObject();  // root
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
