// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Pipeline;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.Twilio;

/// <summary>
/// Sink that sends log events as SMS messages via the Twilio Messages
/// API. Use only for paging-grade events — SMS costs and character
/// limits make this unfit for normal log volume.
/// </summary>
public sealed class TwilioLogSink : HeraldSinkBase, IDisposable, INetworkSink
{
    private const int SmsMaxLength = 1500;

    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly string _from;
    private readonly string _to;
    private readonly bool _ownsHttp;

    public TwilioLogSink(string accountSid, string authToken, string fromNumber, string toNumber, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountSid);
        ArgumentException.ThrowIfNullOrWhiteSpace(authToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(fromNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(toNumber);

        _endpoint = new Uri($"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json", UriKind.Absolute);
        _from = fromNumber;
        _to = toNumber;
        _ownsHttp = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{accountSid}:{authToken}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        var text = $"[{logEvent.Level.Key.ToUpperInvariant()}] {logEvent.Category.Value}: {logEvent.Message}";
        if (text.Length > SmsMaxLength) text = text[..SmsMaxLength] + "…";

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("From", _from),
            new KeyValuePair<string, string>("To", _to),
            new KeyValuePair<string, string>("Body", text),
        });
        using var response = _http.PostAsync(_endpoint, form).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}
