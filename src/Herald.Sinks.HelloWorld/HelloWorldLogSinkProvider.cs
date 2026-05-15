// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Threading;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Output.Rendering;
using MMP.Herald.Routing;

namespace Herald.Sinks.HelloWorld;

/// <summary>
/// Test-only sink provider. Registers a kind so the plugin loader has
/// something concrete to discover and serve through
/// <c>/api/sinkProviders</c>. The actual sink instance is a counter — the
/// minimum surface that proves the provider's <c>CreateSink</c> path is
/// reachable and that hot-add / hot-remove cycles do not leak the dll.
/// </summary>
public sealed class HelloWorldLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "hello_world";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new HelloWorldLogSink();
    }
}

/// <summary>
/// No-op test sink. Increments an in-process counter on every event so a
/// curious operator who attaches a debugger can verify "yes, the sink
/// was actually wired in." Production sinks should not subclass this —
/// it's a discovery / load-context smoke target, not a real ILogger.
/// </summary>
public sealed class HelloWorldLogSink : HeraldSinkBase
{
    private long _eventCount;

    /// <summary>
    /// Total events this instance has accepted since construction.
    /// Surfaced for debugger inspection only — there is no API path to
    /// read it.
    /// </summary>
    public long EventCount => Interlocked.Read(ref _eventCount);

    public override void Log(LogEvent logEvent)
    {
        Interlocked.Increment(ref _eventCount);
    }
}
