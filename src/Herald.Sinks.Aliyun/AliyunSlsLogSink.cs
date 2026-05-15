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
using MMP.Herald.Pipeline;
using MMP.Herald.Services;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.Aliyun;

/// <summary>
/// Sink that sends Herald log events to Alibaba Cloud's Simple Log
/// Service (SLS) via the REST API. Primary target for China-region
/// deployments and any workload that reports into the Aliyun log
/// ecosystem.
/// </summary>
/// <remarks>
/// <para>
/// <b>Wire format.</b>
/// <c>POST https://{project}.{endpoint}/logstores/{logstore}/shards/lb</c>
/// with JSON body. The API version is <c>0.6.0</c> and the request
/// carries a signed <c>Authorization</c> header per Aliyun's signing
/// spec (HMAC-SHA1 over a canonicalised string).
/// </para>
/// <para>
/// <b>Auth.</b> Pass Access Key ID + Access Key Secret. For STS /
/// role-based credentials, supply a pre-signing HttpClient via the
/// code-first ctor and pass empty strings for key/secret — the sink's
/// signing is bypassed when both are blank, letting you layer your
/// own auth handler on top.
/// </para>
/// </remarks>
public sealed class AliyunSlsLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable, INetworkSink
{
    private readonly Uri _endpoint;
    private readonly string _accessKeyId;
    private readonly string _accessKeySecret;
    private readonly string _hostHeader;
    private readonly string _resourcePath;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public AliyunSlsLogSink(
        string endpoint,
        string projectName,
        string logstoreName,
        string accessKeyId,
        string accessKeySecret,
        HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(logstoreName);
        ArgumentNullException.ThrowIfNull(accessKeyId);
        ArgumentNullException.ThrowIfNull(accessKeySecret);

        // Aliyun's endpoint format: https://<region>.log.aliyuncs.com
        // Project-scoped URL: https://<project>.<region>.log.aliyuncs.com/logstores/<logstore>/shards/lb
        var endpointHost = new Uri(endpoint).Host;
        _hostHeader = $"{projectName}.{endpointHost}";
        _resourcePath = $"/logstores/{logstoreName}/shards/lb";
        _endpoint = new Uri($"https://{_hostHeader}{_resourcePath}");

        _accessKeyId = accessKeyId;
        _accessKeySecret = accessKeySecret;
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
        var contentMd5 = ComputeContentMd5(bodyBytes);
        var rfc1123Date = DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture);

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new ByteArrayContent(bodyBytes)
            {
                Headers =
                {
                    { "Content-Type", "application/json" },
                    { "Content-MD5", contentMd5 },
                },
            },
        };

        request.Headers.Add("Host", _hostHeader);
        request.Headers.Add("Date", rfc1123Date);
        request.Headers.Add("x-log-apiversion", "0.6.0");
        request.Headers.Add("x-log-signaturemethod", "hmac-sha1");
        request.Headers.Add("x-log-bodyrawsize", bodyBytes.Length.ToString(CultureInfo.InvariantCulture));

        // Skip signing when both key parts are empty — the caller is
        // layering their own auth handler on top of the HttpClient.
        if (_accessKeyId.Length > 0 && _accessKeySecret.Length > 0)
        {
            var signature = ComputeSignature(contentMd5, rfc1123Date, bodyBytes.Length);
            request.Headers.Add("Authorization", $"LOG {_accessKeyId}:{signature}");
        }

        using var response = _httpClient.Send(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
    }

    private string ComputeSignature(string contentMd5, string date, int bodyRawSize)
    {
        // Aliyun SLS signing (simplified): sign a canonicalised request
        // string with HMAC-SHA1 using the Access Key Secret.
        // See https://help.aliyun.com/document_detail/29012.html
        var canonicalHeaders =
            $"x-log-apiversion:0.6.0\n" +
            $"x-log-bodyrawsize:{bodyRawSize}\n" +
            $"x-log-signaturemethod:hmac-sha1\n";

        var stringToSign =
            "POST\n" +
            contentMd5 + "\n" +
            "application/json\n" +
            date + "\n" +
            canonicalHeaders +
            _resourcePath;

        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(_accessKeySecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
        return Convert.ToBase64String(hash);
    }

    private static string ComputeContentMd5(byte[] body)
    {
        var hash = MD5.HashData(body);
        // Aliyun expects uppercase hex, not base64.
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    private static string BuildBody(IReadOnlyList<LogEvent> events)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("__logs__");
            foreach (var evt in events)
            {
                WriteLog(writer, evt);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteLog(Utf8JsonWriter writer, LogEvent evt)
    {
        writer.WriteStartObject();
        writer.WriteNumber("__time__", evt.TimeUtc.ToUnixTimeSeconds());
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
            foreach (var prop in evt.Properties)
            {
                writer.WriteString(prop.Name, prop.ResolvedValue?.ToString());
            }
        }

        writer.WriteEndObject();
    }
}
