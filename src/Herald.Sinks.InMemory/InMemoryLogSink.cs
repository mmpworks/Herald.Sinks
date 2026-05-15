// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Events;

namespace Herald.Sinks.InMemory;

/// <summary>
/// Sink that retains every log event in-memory for later assertion.
/// Intended for unit and integration tests that want to verify what
/// the pipeline produced — call <see cref="Events"/> to read the
/// captured list or <see cref="Clear"/> to reset between tests.
/// </summary>
/// <remarks>
/// <para>
/// The sink keeps every event forever. Do not use in production —
/// a long-running app will grow the list without bound. For durability
/// pair a file or database sink; for in-memory metrics use the
/// pipeline's metrics hooks.
/// </para>
/// <para>
/// Events are stored by reference; callers should treat them as read-
/// only. <see cref="Events"/> returns a snapshot array, so iterating the
/// snapshot is safe even while more events are being logged on another
/// thread.
/// </para>
/// </remarks>
public sealed class InMemoryLogSink : HeraldSinkBase
{
    private readonly List<LogEvent> _events = new();
    private readonly object _syncRoot = new();

    /// <summary>
    /// Optional cap on retained events. When set and the list reaches
    /// capacity, new events overwrite the oldest. Defaults to unbounded.
    /// </summary>
    public int? Capacity { get; }

    public InMemoryLogSink(int? capacity = null)
    {
        if (capacity is <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive or null.");
        Capacity = capacity;
    }

    /// <summary>
    /// Returns a snapshot of the retained events. Safe to iterate
    /// concurrently with further logging calls — the snapshot is a
    /// fresh array, so it is not affected by subsequent appends.
    /// </summary>
    public IReadOnlyList<LogEvent> Events
    {
        get
        {
            lock (_syncRoot)
            {
                return _events.ToArray();
            }
        }
    }

    /// <summary>Current retained-event count.</summary>
    public int Count
    {
        get
        {
            lock (_syncRoot)
            {
                return _events.Count;
            }
        }
    }

    /// <summary>Discard every retained event. Call between tests.</summary>
    public void Clear()
    {
        lock (_syncRoot)
        {
            _events.Clear();
        }
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        lock (_syncRoot)
        {
            if (Capacity is { } cap && _events.Count >= cap)
            {
                // Drop the oldest event to make room — simple FIFO cap.
                // A List<T>.RemoveAt(0) is O(n) but the expected N is
                // small (test harness use), so we don't reach for a
                // ring buffer here.
                _events.RemoveAt(0);
            }
            _events.Add(logEvent);
        }
    }
}
