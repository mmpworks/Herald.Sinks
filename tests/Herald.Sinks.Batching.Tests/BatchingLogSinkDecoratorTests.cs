// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MMP.Herald;
using MMP.Herald.Events;
using MMP.Herald.Failures;
using MMP.Herald.Pipeline;
using MMP.Herald.Sinks.Batching;
using Xunit;

namespace Herald.Sinks.Batching.Tests;

public sealed class BatchingLogSinkDecoratorTests
{
    private static LogEvent Event(string message = "m") => TestEvents.Make(message);

    private static BatchingOptions Options(
        int batchSize = 4, int flushIntervalMs = 50, int queueCapacity = 1024) =>
        new(Enabled: batchSize > 1, BatchSize: batchSize,
            FlushIntervalMs: flushIntervalMs, QueueCapacity: queueCapacity);

    // ── 1. Events accumulate and flush when BatchSize is reached ─────────

    [Fact]
    public async Task Flushes_a_full_batch_when_BatchSize_is_reached()
    {
        var inner = new RecordingBatchSink();
        var decorator = (BatchingLogSinkDecorator)BatchingLogSinkDecorator.Wrap(
            inner, Options(batchSize: 4, flushIntervalMs: 10_000));

        for (var i = 0; i < 4; i++)
        {
            decorator.Log(Event($"e{i}"));
        }

        // A full batch should flush without waiting for the (10s) timer.
        await inner.WaitForEventCountAsync(4, TimeSpan.FromSeconds(2));

        inner.TotalEvents.Should().Be(4);
        inner.Batches.Should().ContainSingle(b => b.Count == 4);

        await decorator.DisposeAsync();
    }

    // ── 2. Partial batch flushes after FlushIntervalMs ──────────────────

    [Fact]
    public async Task Flushes_a_partial_batch_after_the_flush_interval()
    {
        var inner = new RecordingBatchSink();
        var decorator = (BatchingLogSinkDecorator)BatchingLogSinkDecorator.Wrap(
            inner, Options(batchSize: 100, flushIntervalMs: 50));

        // Only two events — well below the batch size of 100. They should
        // flush on the time window, not the size threshold.
        decorator.Log(Event("a"));
        decorator.Log(Event("b"));

        await inner.WaitForEventCountAsync(2, TimeSpan.FromSeconds(2));

        inner.TotalEvents.Should().Be(2);
        inner.Batches.Should().ContainSingle(b => b.Count == 2);

        await decorator.DisposeAsync();
    }

    // ── 3. Drop-on-overflow: events beyond QueueCapacity are dropped ────

    [Fact]
    public async Task Drops_events_when_the_queue_is_full_and_counts_them()
    {
        // Block the drain inside its first LogBatch so the queue fills behind
        // it. Batch size 1 means the drain forms a batch off the first event
        // and parks immediately inside LogBatch; every later event must sit in
        // the bounded queue (capacity 8) and overflow once it is full.
        var release = new ManualResetEventSlim(false);
        var inner = new BlockingBatchSink(release);

        var decorator = (BatchingLogSinkDecorator)BatchingLogSinkDecorator.Wrap(
            inner, new BatchingOptions(
                Enabled: true, BatchSize: 1, FlushIntervalMs: 10_000, QueueCapacity: 8));

        // Prime one event; the drain forms a size-1 batch and parks in
        // LogBatch. Wait until the sink reports it is genuinely parked so the
        // burst races a stalled drain, not a running one.
        decorator.Log(Event("prime"));
        inner.WaitUntilParked(TimeSpan.FromSeconds(2))
            .Should().BeTrue("the drain must be blocked inside LogBatch before the burst");

        // Burst far more than the queue can hold while the drain is parked.
        const int burst = 200;
        for (var i = 0; i < burst; i++)
        {
            decorator.Log(Event($"e{i}"));
        }

        var total = burst + 1;
        // written + dropped accounts for every Log call.
        (decorator.WrittenCount + decorator.DroppedCount).Should().Be(total);
        decorator.DroppedCount.Should().BeGreaterThan(0);

        release.Set();
        await decorator.DisposeAsync();
    }

    // ── 4. Dispose drains remaining events before returning ─────────────

    [Fact]
    public async Task Dispose_drains_remaining_buffered_events()
    {
        var inner = new RecordingBatchSink();
        var decorator = (BatchingLogSinkDecorator)BatchingLogSinkDecorator.Wrap(
            inner, Options(batchSize: 1_000, flushIntervalMs: 10_000));

        // Below batch size and below the (10s) timer — nothing has flushed
        // yet. Dispose must drain these three before returning.
        decorator.Log(Event("a"));
        decorator.Log(Event("b"));
        decorator.Log(Event("c"));

        await decorator.DisposeAsync();

        inner.TotalEvents.Should().Be(3);
    }

    // ── 5. Enabled=false (batchSize ≤ 1): Wrap returns the bare inner ───

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-5)]
    public void Wrap_returns_the_bare_inner_sink_when_batching_is_disabled(int batchSize)
    {
        var inner = new RecordingBatchSink();
        var options = new BatchingOptions(
            Enabled: false, BatchSize: batchSize, FlushIntervalMs: 1000, QueueCapacity: 8192);

        var result = BatchingLogSinkDecorator.Wrap(inner, options);

        result.Should().BeSameAs(inner);
        result.Should().NotBeOfType<BatchingLogSinkDecorator>();
    }

    [Fact]
    public void From_disables_batching_when_batchSize_is_one_or_less()
    {
        // BatchingOptions.From drives the Enabled flag off the resolved
        // batchSize. A definition that pins batchSize=1 is pass-through.
        var definition = SinkDefinitionWith(("batch_size", 1));
        BatchingOptions.From(definition).Enabled.Should().BeFalse();

        var enabled = SinkDefinitionWith(("batch_size", 256));
        BatchingOptions.From(enabled).Enabled.Should().BeTrue();
    }

    [Fact]
    public void From_falls_back_to_defaults_for_missing_fields()
    {
        // A definition with no batching keys (backwards-compat) gets the
        // Default profile.
        var bare = SinkDefinitionWith();
        var options = BatchingOptions.From(bare);

        options.BatchSize.Should().Be(BatchingOptions.Default.BatchSize);
        options.FlushIntervalMs.Should().Be(BatchingOptions.Default.FlushIntervalMs);
        options.QueueCapacity.Should().Be(BatchingOptions.Default.QueueCapacity);
        options.Enabled.Should().BeTrue();
    }

    [Fact]
    public void From_reads_each_field_from_the_property_bag()
    {
        var definition = SinkDefinitionWith(
            ("batch_size", 512), ("flush_interval_ms", 250), ("queue_capacity", 16_384));

        var options = BatchingOptions.From(definition);

        options.BatchSize.Should().Be(512);
        options.FlushIntervalMs.Should().Be(250);
        options.QueueCapacity.Should().Be(16_384);
    }

    // ── 6. Inner sink throwing: failure sink called per event, drain lives ─

    [Fact]
    public async Task Reports_each_event_to_the_failure_sink_when_LogBatch_throws()
    {
        var inner = new ThrowingBatchSink(new InvalidOperationException("boom"));
        var failures = new RecordingFailureSink();

        var decorator = (BatchingLogSinkDecorator)BatchingLogSinkDecorator.Wrap(
            inner, Options(batchSize: 3, flushIntervalMs: 10_000), failures);

        // First batch of 3 throws; the drain must keep running and process
        // a second batch of 3 that also throws.
        for (var i = 0; i < 6; i++)
        {
            decorator.Log(Event($"e{i}"));
        }

        await failures.WaitForCountAsync(6, TimeSpan.FromSeconds(2));

        // Every event in both failed batches was reported once.
        failures.Count.Should().Be(6);
        failures.Failures.Should().OnlyContain(f => f.Source == "BatchingLogSinkDecorator");
        decorator.BatchErrorCount.Should().BeGreaterThanOrEqualTo(2);

        await decorator.DisposeAsync();
    }

    [Fact]
    public async Task Drain_survives_a_throwing_failure_sink()
    {
        // Defence-in-depth: even if the failure sink itself throws, the
        // drain must not crash — events keep being consumed.
        var inner = new ThrowingBatchSink(new InvalidOperationException("boom"));
        var failures = new ThrowingFailureSink();

        var decorator = (BatchingLogSinkDecorator)BatchingLogSinkDecorator.Wrap(
            inner, Options(batchSize: 2, flushIntervalMs: 50), failures);

        for (var i = 0; i < 4; i++)
        {
            decorator.Log(Event($"e{i}"));
        }

        // Give the drain time to process; it should not have faulted.
        await Task.Delay(300);

        // DisposeAsync completing cleanly proves the drain task is healthy.
        await decorator.DisposeAsync();
        decorator.BatchErrorCount.Should().BeGreaterThan(0);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static MMP.Herald.Configuration.Runtime.LoggingRuntimeSinkDefinition SinkDefinitionWith(
        params (string Key, object Value)[] properties)
    {
        IReadOnlyDictionary<string, object?>? bag = properties.Length == 0
            ? null
            : properties.ToDictionary(p => p.Key, p => (object?)p.Value);

        return new MMP.Herald.Configuration.Runtime.LoggingRuntimeSinkDefinition(
            Name: "test", Kind: "test", Properties: bag);
    }

    // All real sinks implement both IBatchedLogSink and ILogger (via
    // HeraldSinkBase). The fakes mirror that so the disabled-path return
    // (which hands back the bare inner as ILogger) behaves as in production.
    private sealed class RecordingBatchSink : IBatchedLogSink, ILogger
    {
        private readonly object _sync = new();
        private readonly List<IReadOnlyList<LogEvent>> _batches = new();

        public IReadOnlyList<IReadOnlyList<LogEvent>> Batches
        {
            get { lock (_sync) { return _batches.ToList(); } }
        }

        public int TotalEvents
        {
            get { lock (_sync) { return _batches.Sum(b => b.Count); } }
        }

        public void Log(LogEvent logEvent) => LogBatch(new[] { logEvent });

        public void LogBatch(IReadOnlyList<LogEvent> events)
        {
            lock (_sync) { _batches.Add(events); }
        }

        public async Task WaitForEventCountAsync(int count, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (TotalEvents < count && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }
        }
    }

    private sealed class BlockingBatchSink : IBatchedLogSink, ILogger
    {
        private readonly ManualResetEventSlim _release;
        private readonly ManualResetEventSlim _parked = new(false);
        public BlockingBatchSink(ManualResetEventSlim release) => _release = release;

        public void Log(LogEvent logEvent) => LogBatch(new[] { logEvent });

        public void LogBatch(IReadOnlyList<LogEvent> events)
        {
            _parked.Set();      // signal: the drain is now inside LogBatch
            _release.Wait();    // ...and will stay here until released
        }

        public bool WaitUntilParked(TimeSpan timeout) => _parked.Wait(timeout);
    }

    private sealed class ThrowingBatchSink : IBatchedLogSink, ILogger
    {
        private readonly Exception _exception;
        public ThrowingBatchSink(Exception exception) => _exception = exception;

        public void Log(LogEvent logEvent) => LogBatch(new[] { logEvent });
        public void LogBatch(IReadOnlyList<LogEvent> events) => throw _exception;
    }

    private sealed class RecordingFailureSink : ILogFailureSink
    {
        private readonly object _sync = new();
        private readonly List<(LogEvent Event, Exception Exception, string Source)> _failures = new();

        public IReadOnlyList<(LogEvent Event, Exception Exception, string Source)> Failures
        {
            get { lock (_sync) { return _failures.ToList(); } }
        }

        public int Count { get { lock (_sync) { return _failures.Count; } } }

        public void ReportFailure(LogEvent logEvent, Exception exception, string source)
        {
            lock (_sync) { _failures.Add((logEvent, exception, source)); }
        }

        public async Task WaitForCountAsync(int count, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (Count < count && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }
        }
    }

    private sealed class ThrowingFailureSink : ILogFailureSink
    {
        public void ReportFailure(LogEvent logEvent, Exception exception, string source) =>
            throw new InvalidOperationException("failure sink is itself sick");
    }
}
