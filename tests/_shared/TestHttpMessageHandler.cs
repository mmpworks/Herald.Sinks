#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MMP.Herald.Tests.Helpers;

/// <summary>
/// Captures HTTP requests for assertion and returns configurable responses.
/// </summary>
public sealed class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly List<HttpRequestMessage> _requests = new();
    private HttpStatusCode _statusCode = HttpStatusCode.OK;
    private string _responseBody = "";
    private Exception? _exception;

    public IReadOnlyList<HttpRequestMessage> Requests => _requests;
    public int RequestCount => _requests.Count;

    public byte[]? LastRequestBodyBytes { get; private set; }
    public string? LastRequestBodyString { get; private set; }

    public void RespondWith(HttpStatusCode statusCode, string body = "")
    {
        _statusCode = statusCode;
        _responseBody = body;
        _exception = null;
    }

    public void ThrowOnSend(Exception exception)
    {
        _exception = exception;
    }

    protected override HttpResponseMessage Send(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_exception is not null)
        {
            throw _exception;
        }

        if (request.Content is not null)
        {
            LastRequestBodyBytes = request.Content.ReadAsByteArrayAsync(cancellationToken).GetAwaiter().GetResult();
            LastRequestBodyString = System.Text.Encoding.UTF8.GetString(LastRequestBodyBytes);
        }

        _requests.Add(request);

        return new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseBody)
        };
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Send(request, cancellationToken));
    }
}
