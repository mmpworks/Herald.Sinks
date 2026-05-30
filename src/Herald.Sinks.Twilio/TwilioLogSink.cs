// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        using var request = BuildRequest(logEvent);
        // True synchronous send — no captured-context dependency, so this is
        // deadlock-safe on a SynchronizationContext-bearing thread.
        using var response = _http.Send(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();
    }

    public async ValueTask LogAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        using var request = BuildRequest(logEvent);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage BuildRequest(LogEvent logEvent)
    {
        var text = $"[{logEvent.Level.Key.ToUpperInvariant()}] {logEvent.Category.Value}: {logEvent.Message}";
        if (text.Length > SmsMaxLength) text = text[..SmsMaxLength] + "…";

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("From", _from),
            new KeyValuePair<string, string>("To", _to),
            new KeyValuePair<string, string>("Body", text),
        });
        return new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = form };
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}
