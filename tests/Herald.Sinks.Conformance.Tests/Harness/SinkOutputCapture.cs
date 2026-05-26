// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Net.Http;
using MMP.Herald.Events;
using MMP.Herald.Sinks;
using MMP.Herald.Tests.Helpers;

namespace Herald.Sinks.Conformance.Tests.Harness;

/// <summary>
/// Captures the bytes an HTTP-posting sink puts on the wire for one event.
/// The sink is constructed with a <see cref="TestHttpMessageHandler"/>-backed
/// client so nothing leaves the process; the captured body is what an
/// ingester would receive.
/// </summary>
public static class SinkOutputCapture
{
    /// <summary>
    /// Build a sink from a factory that takes a captured <see cref="HttpClient"/>,
    /// log one event through it, and return the request body the sink posted.
    /// </summary>
    public static string CaptureBody(Func<HttpClient, HeraldSinkBase> sinkFactory, LogEvent evt)
    {
        var handler = new TestHttpMessageHandler();
        using var client = new HttpClient(handler);
        var sink = sinkFactory(client);

        try
        {
            sink.Log(evt);
        }
        finally
        {
            (sink as IDisposable)?.Dispose();
        }

        return handler.LastRequestBodyString
            ?? throw new InvalidOperationException("sink posted no request body");
    }
}
