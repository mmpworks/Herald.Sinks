// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MMP.Herald.Tests.Helpers;

/// <summary>
/// Regression harness for the sync-over-async deadlock class fixed in the
/// 2026-05-30 sink async audit. The network sinks used to call
/// <c>SendAsync(...).GetAwaiter().GetResult()</c> with no
/// <c>ConfigureAwait(false)</c>. On a thread that installs a
/// single-threaded <see cref="SynchronizationContext"/> (legacy ASP.NET on
/// System.Web, WPF, WinForms), that pattern deadlocks: the awaited
/// continuation is posted back to the one thread that is blocked waiting on
/// the result.
///
/// <para>
/// The probe reproduces that exact host shape. <see cref="RunUnderSingleThreadContext"/>
/// installs a pumped single-thread context and runs the sink's synchronous
/// <c>Log</c> on it with a timeout. A sink that takes the deadlock never
/// returns and the probe fails on timeout. A sink that uses
/// <c>HttpClient.Send</c> (true sync) or <c>ConfigureAwait(false)</c>
/// completes promptly.
/// </para>
/// </summary>
public static class SyncContextDeadlockProbe
{
    /// <summary>
    /// Default wait before the probe declares a deadlock. Generous — a
    /// healthy sink against the in-memory handler returns in microseconds;
    /// only a real deadlock burns the whole budget.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// An <see cref="HttpClient"/> wired to a handler whose async path
    /// actually yields (<c>await Task.Yield()</c>) before responding. The
    /// yield forces a real continuation — the precondition for the
    /// captured-context deadlock. The sync path returns inline. A sink that
    /// routes its sync <c>Log</c> through <c>HttpClient.Send</c> hits the
    /// inline path and never schedules a continuation; the old
    /// <c>SendAsync().GetResult()</c> path hits the yielding path and
    /// deadlocks under a captured context.
    /// </summary>
    public static HttpClient YieldingClient(HttpStatusCode status = HttpStatusCode.OK) =>
        new(new YieldingHandler(status));

    /// <summary>
    /// Run <paramref name="action"/> on a thread carrying a pumped,
    /// single-threaded <see cref="SynchronizationContext"/> and wait up to
    /// <paramref name="timeout"/> for it to finish. Returns <c>true</c> when
    /// the action completed, <c>false</c> when it deadlocked (timed out).
    /// Rethrows any exception the action itself raised — a network failure
    /// surfacing is not a deadlock.
    /// </summary>
    public static bool RunUnderSingleThreadContext(Action action, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        var wait = timeout ?? DefaultTimeout;

        Exception? captured = null;
        using var done = new ManualResetEventSlim(false);

        // The worker installs a single-threaded context and pumps it. If the
        // action posts a continuation back to this context while blocked, the
        // continuation cannot run (the thread is inside the blocking call) —
        // that is the deadlock we are probing for.
        var worker = new Thread(() =>
        {
            var ctx = new SingleThreadSyncContext();
            SynchronizationContext.SetSynchronizationContext(ctx);
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
            finally
            {
                done.Set();
                ctx.Complete();
            }
        })
        {
            IsBackground = true,
            Name = "deadlock-probe",
        };

        worker.Start();
        var completed = done.Wait(wait);

        if (completed && captured is not null)
        {
            throw new InvalidOperationException(
                "Sink Log threw under the single-thread context (not a deadlock).",
                captured);
        }

        return completed;
    }

    // Handler whose async path yields before responding. The yield is what
    // makes the captured-context deadlock reproducible — without a real
    // continuation, GetResult() on a synchronously-completed task never
    // deadlocks and the regression would be invisible.
    private sealed class YieldingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;

        public YieldingHandler(HttpStatusCode status) => _status = status;

        protected override HttpResponseMessage Send(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            new(_status) { Content = new StringContent("") };

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Yield();
            return new HttpResponseMessage(_status) { Content = new StringContent("") };
        }
    }

    // Minimal pumped single-thread SynchronizationContext. Post() queues the
    // continuation onto a blocking collection that the worker thread drains.
    // While the worker is blocked inside a GetResult(), it is not draining —
    // so a posted continuation sits in the queue forever. That is the
    // deadlock, faithfully reproduced.
    private sealed class SingleThreadSyncContext : SynchronizationContext
    {
        private readonly BlockingCollection<(SendOrPostCallback, object?)> _queue = new();

        public override void Post(SendOrPostCallback d, object? state) =>
            _queue.Add((d, state));

        public override void Send(SendOrPostCallback d, object? state) =>
            d(state);

        public void Complete() => _queue.CompleteAdding();
    }
}
