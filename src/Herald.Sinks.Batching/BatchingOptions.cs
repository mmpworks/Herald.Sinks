// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Configuration.Sinks;

namespace MMP.Herald.Sinks.Batching;

/// <summary>
/// Tuning knobs for <see cref="BatchingLogSinkDecorator"/>. Read from a
/// sink's v2 property bag at <c>CreateSink</c> time and copied into the
/// decorator; the per-event hot path never touches this record.
///
/// <para>
/// <b>Enabled.</b> When <c>false</c>, <see cref="BatchingLogSinkDecorator.Wrap"/>
/// returns the bare inner sink and no decorator is constructed — the sink
/// behaves exactly as it did before. <see cref="From"/> sets this to
/// <c>false</c> when the operator pins <c>batch_size</c> to 1 or below,
/// which is the pass-through escape hatch for a sink that must deliver
/// every event individually.
/// </para>
/// </summary>
public sealed record BatchingOptions(
    bool Enabled,
    int BatchSize,
    int FlushIntervalMs,
    int QueueCapacity)
{
    // Property-bag keys. Match the field names declared in each sink's
    // configuration*.mmpform __properties block.
    private const string KeyBatchSize       = "batch_size";
    private const string KeyFlushIntervalMs = "flush_interval_ms";
    private const string KeyQueueCapacity   = "queue_capacity";

    /// <summary>
    /// Default batching profile: batch up to 256 events, hold a partial
    /// batch no longer than one second, queue up to 8192 events before
    /// drop-on-overflow. These match the mmpform defaults so a sink whose
    /// operator never touches the batching fields runs the same numbers
    /// the form would have produced.
    /// </summary>
    public static readonly BatchingOptions Default = new(
        Enabled: true, BatchSize: 256, FlushIntervalMs: 1000, QueueCapacity: 8192);

    /// <summary>
    /// Build options from a sink's runtime definition. Reads
    /// <c>batch_size</c>, <c>flush_interval_ms</c>, and <c>queue_capacity</c>
    /// from <see cref="LoggingRuntimeSinkDefinition.Properties"/>, falling
    /// back to <see cref="Default"/> for any field the operator left unset
    /// (backwards-compat: a sink configured before the batching fields
    /// existed gets the defaults). Returns <see cref="Default"/> wholesale
    /// when the definition is null.
    ///
    /// <para>
    /// <b>Disable rule.</b> A resolved <c>batch_size &lt;= 1</c> produces an
    /// options record with <see cref="Enabled"/> set to <c>false</c> —
    /// <see cref="BatchingLogSinkDecorator.Wrap"/> then hands back the bare
    /// inner sink. One event per batch is no batching at all; treating it
    /// as pass-through keeps a degenerate config off the background-drain
    /// path entirely.
    /// </para>
    /// </summary>
    public static BatchingOptions From(LoggingRuntimeSinkDefinition? definition)
    {
        if (definition is null) return Default;

        var bag = definition.Properties;
        var batchSize       = SinkPropertyBag.ReadInt(bag, KeyBatchSize)       ?? Default.BatchSize;
        var flushIntervalMs = SinkPropertyBag.ReadInt(bag, KeyFlushIntervalMs) ?? Default.FlushIntervalMs;
        var queueCapacity   = SinkPropertyBag.ReadInt(bag, KeyQueueCapacity)   ?? Default.QueueCapacity;

        return new BatchingOptions(
            Enabled: batchSize > 1,
            BatchSize: batchSize,
            FlushIntervalMs: flushIntervalMs,
            QueueCapacity: queueCapacity);
    }
}
