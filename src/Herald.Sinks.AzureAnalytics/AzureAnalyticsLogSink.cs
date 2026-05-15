// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;

namespace Herald.Sinks.AzureAnalytics;

/// <summary>
/// Sink that writes log events to an Azure Log Analytics workspace via
/// the HTTP Data Collector API. Drop-in for
/// <c>Serilog.Sinks.AzureAnalytics</c>. No Azure SDK dependency — the
/// sink signs requests inline with HMAC-SHA256.
/// </summary>
/// <remarks>
/// <para>
/// <b>Wire format.</b>
/// <c>POST https://{workspaceId}.ods.opinsights.azure.com/api/logs?api-version=2016-04-01</c>
/// with a base64-signed <c>Authorization</c> header and JSON body. Body
/// is a JSON array of records; Log Analytics indexes them under the
/// <c>Log-Type</c> header value, appending <c>_CL</c> (custom log) to
/// the type name at query time.
/// </para>
/// <para>
/// <b>Auth.</b> The workspace ID and primary/secondary key pair
/// identify the workspace and sign the request. The key is a base64
/// string; the sink base64-decodes it and uses the raw bytes as the
/// HMAC key. Rotate keys on the Azure side and reconstruct the sink
/// — there is no refresh hook.
/// </para>
/// <para>
/// <b>Batching.</b> Implements <see cref="IBatchedLogSink"/> so the
/// pipeline's batching step packs events into single POSTs. Data
/// Collector's max payload is 30 MB; at roughly ~1KB/event this comes
/// to ~30,000 events per batch, far above typical pipeline sizing.
/// </para>
/// </remarks>
public sealed class AzureAnalyticsLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable
{
    private const string ApiVersion = "2016-04-01";
    private const string ResourcePath = "/api/logs";

    private readonly Uri _endpoint;
    private readonly byte[] _workspaceKey;
    private readonly string _workspaceId;
    private readonly string _logType;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public AzureAnalyticsLogSink(
        string workspaceId,
        string workspaceKey,
        string logType = "HeraldLog",
        HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(logType);
        if (!IsValidLogTypeName(logType))
        {
            throw new ArgumentException(
                "Log-Type must be alphanumeric with no spaces or special characters. " +
                "Azure appends _CL automatically at query time.",
                nameof(logType));
        }

        _workspaceId = workspaceId;
        _workspaceKey = Convert.FromBase64String(workspaceKey);
        _logType = logType;
        _endpoint = new Uri(
            $"https://{workspaceId}.ods.opinsights.azure.com{ResourcePath}?api-version={ApiVersion}");

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
        var bodyBytes = Encoding.UTF8.GetBytes(body);

        // RFC1123 date in GMT — required header, part of the signature.
        var rfc1123Date = DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture);
        var signature = BuildSignature(bodyBytes.Length, rfc1123Date);

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new ByteArrayContent(bodyBytes)
            {
                Headers = { { "Content-Type", "application/json" } },
            },
        };

        request.Headers.Add("Authorization", signature);
        request.Headers.Add("Log-Type", _logType);
        request.Headers.Add("x-ms-date", rfc1123Date);
        request.Headers.Add("time-generated-field", "time_utc");

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
            writer.WriteStartArray();
            foreach (var evt in events)
            {
                WriteRecord(writer, evt);
            }
            writer.WriteEndArray();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteRecord(Utf8JsonWriter writer, LogEvent evt)
    {
        writer.WriteStartObject();
        writer.WriteString("time_utc", evt.TimeUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteString("level", evt.Level.Key);
        writer.WriteString("category", evt.Category.Value);
        writer.WriteString("message", evt.Message ?? string.Empty);
        writer.WriteString("template", evt.MessageTemplate ?? string.Empty);

        if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value) && value is Exception ex)
        {
            writer.WriteString("exception", ex.ToString());
            writer.WriteString("exception_type", ex.GetType().FullName ?? ex.GetType().Name);
        }

        if (evt.Properties is not null && evt.Properties.Count > 0)
        {
            // Log Analytics flattens property keys into fields on the
            // record. Avoid nested objects in 1.0 — the query layer
            // handles flat columns much better than deep structures.
            foreach (var prop in evt.Properties)
            {
                writer.WriteString("prop_" + prop.Name, prop.ResolvedValue?.ToString());
            }
        }

        writer.WriteEndObject();
    }

    private string BuildSignature(int contentLength, string rfc1123Date)
    {
        // Data Collector signing per
        // https://learn.microsoft.com/azure/azure-monitor/logs/data-collector-api
        var stringToSign =
            "POST\n" +
            contentLength.ToString(CultureInfo.InvariantCulture) + "\n" +
            "application/json\n" +
            "x-ms-date:" + rfc1123Date + "\n" +
            ResourcePath;

        var bytes = Encoding.UTF8.GetBytes(stringToSign);
        using var hmac = new HMACSHA256(_workspaceKey);
        var hash = hmac.ComputeHash(bytes);
        return $"SharedKey {_workspaceId}:{Convert.ToBase64String(hash)}";
    }

    private static bool IsValidLogTypeName(string name)
    {
        // Azure requires Log-Type to be alphanumeric, 1-100 chars.
        if (name.Length is 0 or > 100) return false;
        foreach (var c in name)
        {
            if (!char.IsLetterOrDigit(c)) return false;
        }
        return true;
    }
}
