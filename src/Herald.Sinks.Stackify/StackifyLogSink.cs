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

namespace Herald.Sinks.Stackify;

/// <summary>
/// Sink that forwards Herald log events to the Stackify Retrace
/// logs API. Drop-in for Serilog.Sinks.Stackify. Pure HTTP — no
/// Stackify SDK.
/// </summary>
public sealed class StackifyLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable
{
    private static readonly Uri Endpoint = new("https://api.stackify.com/Log/Save");

    private readonly string _apiKey;
    private readonly string _appName;
    private readonly string _environmentName;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public StackifyLogSink(
        string apiKey,
        string appName = "herald",
        string environmentName = "production",
        HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);

        _apiKey = apiKey;
        _appName = appName;
        _environmentName = environmentName;
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

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("X-Stackify-Key", _apiKey);

        using var response = _httpClient.Send(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
    }

    private string BuildBody(IReadOnlyList<LogEvent> events)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("CDID", Environment.MachineName);
            writer.WriteString("CDAppID", _appName);
            writer.WriteString("AppName", _appName);
            writer.WriteString("Env", _environmentName);
            writer.WriteString("ServerName", Environment.MachineName);

            writer.WriteStartArray("Msgs");
            foreach (var evt in events)
            {
                WriteMsg(writer, evt);
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteMsg(Utf8JsonWriter writer, LogEvent evt)
    {
        writer.WriteStartObject();
        writer.WriteString("Msg", evt.Message ?? string.Empty);
        writer.WriteNumber("EpochMs", evt.TimeUtc.ToUnixTimeMilliseconds());
        writer.WriteString("Level", MapLevel(evt.Level.Key));
        writer.WriteString("SrcMethod", evt.Category.Value);

        if (evt.Properties is not null && evt.Properties.Count > 0)
        {
            using var propsStream = new MemoryStream();
            using (var propsWriter = new Utf8JsonWriter(propsStream))
            {
                propsWriter.WriteStartObject();
                foreach (var prop in evt.Properties)
                {
                    propsWriter.WriteString(prop.Name, prop.ResolvedValue?.ToString());
                }
                propsWriter.WriteEndObject();
                propsWriter.Flush();
            }
            writer.WriteString("data", Encoding.UTF8.GetString(propsStream.ToArray()));
        }

        if (evt.Context.TryGetValue(LogContextKeys.Exception, out var v) && v is Exception ex)
        {
            writer.WriteStartObject("Ex");
            writer.WriteString("OccurredEpochMillis", evt.TimeUtc.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
            writer.WriteStartObject("Error");
            writer.WriteString("Message", ex.Message);
            writer.WriteString("ErrorType", ex.GetType().FullName ?? ex.GetType().Name);
            writer.WriteString("StackTrace", ex.StackTrace ?? string.Empty);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static string MapLevel(string levelKey) => levelKey switch
    {
        "trace" or "debug" => "DEBUG",
        "info" or "notice" or "success" => "INFO",
        "warn" => "WARN",
        "error" => "ERROR",
        "critical" or "security" => "FATAL",
        _ => "INFO",
    };
}
