<!--
  Copyright (c) 2026 MMP LLC
  Licensed under the MIT License. See LICENSE in the project root.
-->

# Herald.Sinks.PeriodicBatching

**This is not a NuGet package.** It is a migration guide.

If you searched for `Herald.Sinks.PeriodicBatching` looking for
Herald's answer to `Serilog.Sinks.PeriodicBatching`, you are in the
right place. Herald does not ship a `PeriodicBatching` sink because
batching is a pipeline feature in Herald — one switch, works for every
sink.

---

## What Serilog.Sinks.PeriodicBatching does

`Serilog.Sinks.PeriodicBatching` is a **base class** network sinks
derive from. It:

1. Holds events in a memory queue.
2. Fires a timer every N seconds.
3. When the timer fires (or the queue fills up), it sends the whole
   batch to the real sink in one call.

Almost every Serilog network sink (Datadog, Seq, Splunk, Elasticsearch,
HTTP) uses it behind the scenes. Your app rarely touches it directly —
but its configuration (batch size, flush interval, queue limit) lives
on each sink you add to the pipeline.

Typical shape (you see this indirectly):

```csharp
// Serilog — the batching config leaks into each sink's own config
Log.Logger = new LoggerConfiguration()
    .WriteTo.Datadog(apiKey,
        configuration: new DatadogConfiguration { /* ... */ },
        exceptionHandler: null,
        // batching knobs, one copy per sink:
        batchPostingLimit: 100,
        period: TimeSpan.FromSeconds(2),
        queueLimit: 10000)
    .CreateLogger();
```

---

## What Herald does instead

Herald treats batching as a **pipeline step**, not a per-sink wrapper.
One call on `QuickLogBuilder` turns batching on for every sink that
implements `IBatchedLogSink`. Each sink declares whether it accepts
batches; Herald routes one batch per sink, sized and timed by the
pipeline's single batching config.

That means:

- You configure batch size and delay **once**, not once per sink.
- Sinks that don't implement batching still work — Herald hands them
  events one at a time, no wrapping needed.
- Adding a new sink inherits the same batching shape without touching
  its config.

---

## Migration, step by step

### Goal: batch every network sink with a 2-second flush interval

You currently log to Datadog and Seq via Serilog with batching. You
want Herald to do the same thing.

#### Step 1. Register the sinks you want

```csharp
using MMP.Herald.Quick;

var logger = QuickLogBuilder
    .Create("app")
    .WithMinimumLevel("info")
    .WithDatadogSink(apiKey: "dd-api-key")
    .WithSeqSink(serverUrl: "https://seq.example.com")
    .WithConsoleSink()   // not batched — console gets events immediately
    .BuildAndCommit()
    .Logger;
```

#### Step 2. Turn batching on once for the whole pipeline

```csharp
var logger = QuickLogBuilder
    .Create("app")
    .WithMinimumLevel("info")
    .WithDatadogSink(apiKey: "dd-api-key")
    .WithSeqSink(serverUrl: "https://seq.example.com")
    .WithConsoleSink()
    // One line. Applies to every sink that supports batching.
    .WithBatching(maxBatchSize: 100, maxBatchDelayMs: 2000)
    .BuildAndCommit()
    .Logger;
```

That's the whole migration. The Datadog and Seq sinks receive events
in batches of up to 100 or every 2 seconds, whichever comes first.
The console sink still gets events one at a time (it's a streaming
sink, not a batching sink).

#### Step 3. Tune the numbers

Two knobs:

- **`maxBatchSize`** — flush when the pending count reaches this. Good
  starting value for HTTP sinks is 32–128.
- **`maxBatchDelayMs`** — flush if no event has arrived for this long.
  Keeps the tail of a quiet service from hanging. 500–2000 ms is a
  common range.

Rule of thumb: the bigger the batch, the better the throughput. The
longer the delay, the staler the data looks in your observability
tool. Pick based on your visibility requirement, not a default.

---

## What's NEW and IMPROVED in the Herald approach

| Serilog.Sinks.PeriodicBatching | Herald batching |
|---|---|
| Every network sink implements its own batching by deriving from the base class | Batching is a single pipeline step applied above the sinks |
| Batch size / interval specified per sink | Specified once; every batched sink picks it up |
| Each sink carries its own memory queue | One queue across the pipeline |
| No way to turn off for a specific sink while keeping it on elsewhere | Sinks that do not implement `IBatchedLogSink` get events one at a time automatically |
| Hard to add a new batched sink — must inherit the base class, handle the abstract `EmitBatchAsync` method, wire the disposal correctly | Add a sink that implements `IBatchedLogSink`, that's it |
| Batching state invisible from the outside | Pipeline introspection exposes current batch size, queue depth, and flush rate via `PipelineAccessor` |

**Bonus:** Herald's batching lives below the kernel fast path. When
the pipeline is kernel-eligible, events flow into the batch buffer
without going through the full decorator chain first — meaningfully
cheaper per event than Serilog's per-sink wrapper approach.

---

## Full working example

```csharp
using MMP.Herald.Quick;

var logger = QuickLogBuilder
    .Create("app")
    .WithMinimumLevel("info")
    // Three sinks: two batched network sinks, one streaming console.
    .WithDatadogSink(apiKey: Environment.GetEnvironmentVariable("DD_API_KEY")!)
    .WithSeqSink(serverUrl: "https://seq.example.com")
    .WithConsoleSink()
    // Batch size 64, flush every 1 second. Applies to Datadog + Seq.
    // Console ignores it — console always streams.
    .WithBatching(maxBatchSize: 64, maxBatchDelayMs: 1000)
    .BuildAndCommit()
    .Logger;

// Use the logger normally. Events queue up for the batched sinks,
// flush on size or interval, and stream to console immediately.
logger.Info("orders", "order {OrderId} submitted", 42);
logger.Warn("orders", "inventory low for {Sku}", "abc-123");
```

---

## Troubleshooting

**Events aren't showing up in Datadog.**
Batching delays network sends. If `maxBatchDelayMs` is 10000, you
won't see an event for up to 10 seconds after it fires. Lower the
delay or emit enough events to fill a batch.

**I want events to flush immediately on shutdown.**
Herald's pipeline dispose flushes pending batches. Call
`logger.Dispose()` (or `pipeline.Dispose()`) on shutdown. If you're
using `QuickLogBuilder.BuildAndCommit()`, the returned bundle is
`IDisposable`.

**One sink needs a different batch size.**
The pipeline-wide setting is a deliberate simplification. If you truly
need per-sink batching, wrap that single sink in its own pipeline and
bridge events across. In practice, tuning one pipeline-wide value
covers almost every real deployment.

**The batch is flushing too often under low traffic.**
Lower `maxBatchSize` keeps batches small even under load; raise
`maxBatchDelayMs` to let the buffer collect more before flushing when
events trickle in.

---

## See also

- `Modules/Core/docs/under-the-hood.md` Section 5 — pipeline data outputs
- `Modules/Core/src/Pipeline/BatchingLogger.cs` — the batching implementation
- `IBatchedLogSink` — the sink contract that receives `LogBatch`
- `QuickLogBuilder.WithBatching()` — the pipeline step
