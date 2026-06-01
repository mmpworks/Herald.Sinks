// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MMP.Herald.Events;
using MMP.Herald.Failures;
using MMP.Herald.Pipeline;

namespace MMP.Herald.Sinks.Batching;

/// <summary>
/// Wraps an <see cref="IBatchedLogSink"/> so the producer never blocks on
/// delivery. Events ride a bounded <see cref="Channel{T}"/>; a single
/// background drain accumulates them into batches — up to
/// <see cref="BatchingOptions.BatchSize"/> events, or whatever has arrived
/// when <see cref="BatchingOptions.FlushIntervalMs"/> elapses — and hands
/// each batch to the inner sink's <see cref="IBatchedLogSink.LogBatch"/>.
///
/// <para>
/// <b>Why this exists.</b> Every Herald network sink implements
/// <see cref="IBatchedLogSink"/>, but the chain calls <see cref="ILogger.Log"/>
/// once per event, and each network sink's <c>Log</c> does one blocking
/// round-trip. At rate that round-trip stalls the producer. This decorator
/// is the shared seam that turns per-event delivery into batched delivery
/// without changing any sink's own code — every network sink wraps itself
/// with one <see cref="Wrap"/> call in its provider's <c>CreateSink</c>.
/// </para>
///
/// <para>
/// <b>Producer never blocks.</b> <see cref="Log"/> does a non-blocking
/// <c>TryWrite</c> and returns. When the bounded queue is full the event is
/// dropped and <see cref="DroppedCount"/> increments — the producer is never
/// parked on a slow sink. Single reader (the drain), many writers (any
/// producer thread).
/// </para>
///
/// <para>
/// <b>Failure is reported, never re-thrown.</b> When
/// <see cref="IBatchedLogSink.LogBatch"/> throws, the decorator reports the
/// failure once per event through the <see cref="ILogFailureSink"/> (when
/// one was wired) and the drain keeps running. A sick sink degrades to
/// dropped events plus failure reports; it never starves the drain or
/// crashes the host.
/// </para>
///
/// <para>
/// <b>Disposal.</b> <see cref="DisposeAsync"/> completes the writer, drains
/// the backlog within <see cref="DefaultDrainTimeout"/>, and falls through
/// to cancellation if the drain does not finish in time so a hung inner
/// sink cannot pin the instance forever. The shape mirrors
/// <c>FastPathAsyncSink</c>.
/// </para>
/// </summary>
public sealed class BatchingLogSinkDecorator : ILogger, IAsyncDisposable
{
    private const string FailureSource = "BatchingLogSinkDecorator";

    private readonly IBatchedLogSink _inner;
    private readonly ILogFailureSink? _failureSink;
    private readonly int _batchSize;
    private readonly TimeSpan _flushInterval;
    private readonly Channel<LogEvent> _channel;
    private readonly CancellationTokenSource _cts;
    private readonly Task _drain;

    private long _droppedCount;
    private long _writtenCount;
    private long _batchErrorCount;
    private volatile bool _disposed;

    /// <summary>
    /// Default drain timeout used by <see cref="DisposeAsync"/>. Matches
    /// the FastPathAsyncSink wait — long enough for a healthy inner sink to
    /// clear a backlog, short enough that a hung sink does not block
    /// teardown indefinitely.
    /// </summary>
    public static readonly TimeSpan DefaultDrainTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Wrap <paramref name="inner"/> with batching when
    /// <paramref name="options"/> are enabled; otherwise return the bare
    /// inner sink. Returning the inner directly when
    /// <see cref="BatchingOptions.Enabled"/> is <c>false</c> keeps a
    /// pass-through config (batchSize ≤ 1) off the background-drain path
    /// entirely — no channel, no drain task, no behaviour change.
    /// </summary>
    public static ILogger Wrap(
        IBatchedLogSink inner,
        BatchingOptions options,
        ILogFailureSink? failureSink = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(options);

        // The inner sink is always an ILogger too (every IBatchedLogSink in
        // the sink fleet derives from HeraldSinkBase). Returning it as the
        // bare path requires it to implement ILogger; guard so a future
        // batched-only sink fails loudly here instead of silently dropping.
        if (!options.Enabled)
        {
            return inner as ILogger
                ?? throw new ArgumentException(
                    "Batching is disabled (batchSize ≤ 1) but the inner sink does not " +
                    "implement ILogger, so it cannot be used as the pass-through path.",
                    nameof(inner));
        }

        return new BatchingLogSinkDecorator(inner, options, failureSink);
    }

    private BatchingLogSinkDecorator(
        IBatchedLogSink inner,
        BatchingOptions options,
        ILogFailureSink? failureSink)
    {
        _inner = inner;
        _failureSink = failureSink;
        _batchSize = options.BatchSize;
        _flushInterval = TimeSpan.FromMilliseconds(options.FlushIntervalMs);

        // Bounded queue, drop-on-overflow, producer never blocks. We use
        // FullMode.Wait rather than FullMode.DropWrite deliberately: under
        // DropWrite the BCL silently drops the incoming item AND returns
        // true from TryWrite, so an overflow is uncountable — DroppedCount
        // could never move. With Wait mode, a non-blocking TryWrite returns
        // FALSE when the queue is full; we then drop the event ourselves and
        // increment DroppedCount. Same observable behaviour (newest event
        // dropped on overflow, producer never blocks because we never call
        // the blocking WriteAsync), but the drop is now visible to operators.
        // SingleReader (the one drain task) lets the channel skip its
        // multi-reader synchronisation.
        _channel = Channel.CreateBounded<LogEvent>(new BoundedChannelOptions(options.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
        _cts = new CancellationTokenSource();
        _drain = Task.Run(DrainAsync);
    }

    /// <summary>Total events successfully enqueued.</summary>
    public long WrittenCount => Interlocked.Read(ref _writtenCount);

    /// <summary>Total events dropped because the queue was full.</summary>
    public long DroppedCount => Interlocked.Read(ref _droppedCount);

    /// <summary>
    /// Total batches that failed to deliver. Increments once per failed
    /// <see cref="IBatchedLogSink.LogBatch"/> call; each event in the failed
    /// batch is also surfaced through the failure sink when one is wired.
    /// </summary>
    public long BatchErrorCount => Interlocked.Read(ref _batchErrorCount);

    /// <summary>
    /// Enqueue an event and return. Never blocks: a full queue drops the
    /// event and increments <see cref="DroppedCount"/>.
    /// </summary>
    public void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        if (_channel.Writer.TryWrite(logEvent))
        {
            Interlocked.Increment(ref _writtenCount);
        }
        else
        {
            Interlocked.Increment(ref _droppedCount);
        }
    }

    // Drain loop: pull events, accumulate up to _batchSize OR until the
    // flush window elapses with a partial batch in hand, then flush. The
    // size-or-time wait is the same shape TimedBatchBuffer uses, expressed
    // over the channel reader.
    //
    // Cognitive Complexity note: the loop has two nested reads. The outer
    // WaitToReadAsync blocks (cheaply) until at least one event is queued
    // or the channel completes. Once an event is available, FillBatch pulls
    // up to _batchSize-1 more, bounded by the flush window, then we flush.
    // Splitting FillBatch out keeps the outer loop straight-line.
    private async Task DrainAsync()
    {
        var token = _cts.Token;
        var batch = new List<LogEvent>(_batchSize);

        try
        {
            while (await _channel.Reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                await FillBatchAsync(batch, token).ConfigureAwait(false);
                if (batch.Count > 0)
                {
                    FlushBatch(batch);
                    batch.Clear();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown. Any events still buffered locally are flushed
            // by the finally block so a cancel mid-accumulate does not lose
            // the partial batch.
        }
        finally
        {
            DrainRemaining(batch);
        }
    }

    // Accumulate into batch until it reaches _batchSize or the flush window
    // expires. The window is a linked CTS that fires after _flushInterval;
    // when it trips we stop accumulating and let the caller flush whatever
    // we have. Reads that find the channel drained or completed also return.
    private async Task FillBatchAsync(List<LogEvent> batch, CancellationToken token)
    {
        // Drain whatever is already queued without waiting first — this is
        // the common high-rate case where a full batch is sitting ready.
        while (batch.Count < _batchSize && _channel.Reader.TryRead(out var evt))
        {
            batch.Add(evt);
        }

        if (batch.Count >= _batchSize || batch.Count == 0)
        {
            return;
        }

        // Partial batch in hand: wait up to the flush window for more before
        // giving up and flushing what we have.
        using var window = CancellationTokenSource.CreateLinkedTokenSource(token);
        window.CancelAfter(_flushInterval);

        try
        {
            while (batch.Count < _batchSize &&
                   await _channel.Reader.WaitToReadAsync(window.Token).ConfigureAwait(false))
            {
                while (batch.Count < _batchSize && _channel.Reader.TryRead(out var evt))
                {
                    batch.Add(evt);
                }
            }
        }
        catch (OperationCanceledException) when (window.IsCancellationRequested && !token.IsCancellationRequested)
        {
            // Flush window elapsed — flush the partial batch. Distinct from
            // a real shutdown cancel (token), which rethrows to the outer
            // loop's handler.
        }
    }

    // Pull and flush every remaining event after the channel completes or a
    // shutdown cancel. Includes any partial batch the drain loop was holding.
    private void DrainRemaining(List<LogEvent> carryOver)
    {
        var batch = carryOver;
        while (_channel.Reader.TryRead(out var evt))
        {
            batch.Add(evt);
            if (batch.Count >= _batchSize)
            {
                FlushBatch(batch);
                batch = new List<LogEvent>(_batchSize);
            }
        }

        if (batch.Count > 0)
        {
            FlushBatch(batch);
        }
    }

    // Forward one batch to the inner sink. On failure, report once per event
    // through the failure sink (mirrors TimedBatchBuffer.FlushBatch) and keep
    // the drain alive — never re-throw.
    private void FlushBatch(IReadOnlyList<LogEvent> batch)
    {
        if (batch.Count == 0) return;

        // Snapshot the batch so a later Clear() on the drain's working list
        // cannot mutate what the inner sink may still hold a reference to.
        var snapshot = new List<LogEvent>(batch);

        try
        {
            _inner.LogBatch(snapshot);
        }
        catch (Exception exception)
        {
            Interlocked.Increment(ref _batchErrorCount);
            ReportBatchFailure(snapshot, exception);
        }
    }

    private void ReportBatchFailure(IReadOnlyList<LogEvent> batch, Exception exception)
    {
        if (_failureSink is null) return;

        foreach (var logEvent in batch)
        {
            try { _failureSink.ReportFailure(logEvent, exception, FailureSource); }
            catch { /* failure-sink itself failed; swallow to keep the drain alive */ }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Complete the writer so the drain loop's WaitToReadAsync returns
        // false once the queue empties; the drain then flushes the backlog
        // and exits. TryComplete is idempotent.
        _channel.Writer.TryComplete();

        try
        {
            await _drain.WaitAsync(DefaultDrainTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Inner sink hung past the timeout. Cancel the drain so it does
            // not pin the instance, then let teardown finish.
            _cts.Cancel();
            try { await _drain.ConfigureAwait(false); } catch { /* shutdown */ }
        }

        _cts.Dispose();
        await DisposeInnerAsync().ConfigureAwait(false);
    }

    // Dispose an inner sink that owns disposable resources (HttpClient,
    // SDK clients). The decorator owns the inner's lifetime once it wraps
    // it, so it is the right place to release it. Runs on every teardown
    // path — clean drain and timed-out drain alike.
    private async ValueTask DisposeInnerAsync()
    {
        if (_inner is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (_inner is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
