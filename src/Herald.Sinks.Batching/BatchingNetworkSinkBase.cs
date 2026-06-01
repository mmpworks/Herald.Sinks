// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System.Collections.Generic;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Sinks;

namespace MMP.Herald.Sinks.Batching;

/// <summary>
/// Base class for network sinks that have no native batch API. Derives from
/// <see cref="HeraldSinkBase"/> and adds a default <see cref="IBatchedLogSink"/>
/// implementation, so a sink only has to override <see cref="HeraldSinkBase.Log(LogEvent)"/>
/// and it is immediately batch-capable.
///
/// <para>
/// <b>Why this exists.</b> <see cref="BatchingLogSinkDecorator"/> hands each
/// batch to the inner sink's <see cref="IBatchedLogSink.LogBatch"/>. A sink
/// whose destination has no bulk endpoint still benefits from the decorator —
/// the producer stops blocking on per-event delivery — but it needs a
/// <c>LogBatch</c> to forward to. The default loop below replays the batch one
/// event at a time through the sink's existing <c>Log</c> override. The win is
/// the non-blocking producer plus a single drain thread, not a bulk wire call.
/// Sinks whose destination <i>does</i> have a bulk endpoint override
/// <see cref="LogBatch"/> with the native call.
/// </para>
/// </summary>
public abstract class BatchingNetworkSinkBase : HeraldSinkBase, IBatchedLogSink
{
    /// <summary>
    /// Default batch handler: forward each event to <see cref="HeraldSinkBase.Log(LogEvent)"/>.
    /// Runs on the decorator's drain thread, so the per-event blocking write no
    /// longer parks the producer. Override when the destination offers a native
    /// bulk endpoint that should receive the whole batch in one call.
    /// </summary>
    public virtual void LogBatch(IReadOnlyList<LogEvent> events)
    {
        foreach (var e in events)
        {
            Log(e);
        }
    }
}
