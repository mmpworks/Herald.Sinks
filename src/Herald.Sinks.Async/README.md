<!--
  Copyright (c) 2026 MMPWorks LLC
  Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
-->

# Herald.Sinks.Async

**This is not a NuGet package.** It is a migration guide.

If you searched for `Herald.Sinks.Async` looking for Herald's answer
to `Serilog.Sinks.Async`, you are in the right place. Herald does not
ship an `Async` sink because async delivery is a pipeline feature in
Herald — one switch, applies to every sink.

---

## What Serilog.Sinks.Async does

`Serilog.Sinks.Async` is a **wrapper sink** you put around your other
sinks. Events arrive on the calling thread, get pushed into an
in-memory queue, and then a background thread drains the queue by
calling the real sink.

Why you'd want it:

- A slow sink (HTTP, database) blocks the calling thread. Your
  application's request handler waits for the log write. Bad.
- Wrap the slow sink in `Async`, and the caller returns as soon as
  the event is in the queue. The background worker does the slow
  work off the hot path.

Typical shape:

```csharp
// Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Async(a => a.File("logs/app.log"), bufferSize: 10000)
    .CreateLogger();
```

Only the wrapped sinks run on the background thread. Any unwrapped
sinks in the same pipeline keep writing synchronously.

---

## What Herald does instead

Herald builds async delivery into the pipeline itself. One call on
`QuickLogBuilder` turns the whole pipeline async — every sink runs on
the background worker, every log call returns as soon as the event is
on the queue.

The advantages:

- **One switch for everything.** You don't wrap sinks individually.
- **Two drop strategies.** Choose what happens when the queue fills
  up — drop silently (for non-critical telemetry) or block with a
  timeout (for audit-grade events).
- **Introspection.** Your health endpoint can read the current queue
  depth so you know if sinks are falling behind.
- **Failure visibility.** Dropped events flow to Herald's failure
  sink channel, so a drop is always explained.

---

## Migration, step by step

### Goal: get log writes off the request-handling thread

Your API is writing to a file sink (or Seq, or Datadog) on the same
thread that handles each HTTP request. Profiles show the log writes
on the critical path. Move them to a background worker.

#### Step 1. Add the async step to your builder

```csharp
using MMP.Herald.Quick;
using MMP.Herald.Services;

var logger = QuickLogBuilder
    .Create("app")
    .WithMinimumLevel("info")
    .WithFileSink("logs/app.log")
    .WithDatadogSink(apiKey: "dd-api-key")
    // One line. Every log call now returns after queueing.
    .WithAsyncLogging(
        capacity: 4096,
        dropStrategy: KnownDropStrategies.DropWrite)
    .BuildAndCommit()
    .Logger;
```

#### Step 2. Choose a drop strategy

Two knobs matter when the queue fills up:

1. **`DropWrite`** — if the queue is full, silently drop the new
   event. The caller returns immediately. Good for per-frame game
   logs, high-frequency telemetry, and anywhere latency matters more
   than having every event.

2. **`Wait`** — if the queue is full, the caller blocks until there's
   room. Herald's default wait is 100 ms; after that the event
   drops (with a different `DropReason` so you can tell it apart in
   the failure sink). Good for audit logs, security events, or
   anything you can't afford to lose without at least *trying*.

If you're not sure which one, start with `DropWrite`.

```csharp
// Non-blocking — drop events when the queue is full. Hot-path safe.
.WithAsyncLogging(
    capacity: 4096,
    dropStrategy: KnownDropStrategies.DropWrite)

// Blocking with timeout — try hard to keep every event.
.WithAsyncLogging(
    capacity: 4096,
    dropStrategy: KnownDropStrategies.Wait)
```

#### Step 3. Size the queue

`capacity` is the maximum number of pending events. Guidance:

- **Under 1000** — tight memory budget, low-volume service. Small
  queue means drops kick in earlier under load.
- **4096 (default)** — middle of the road. Handles traffic spikes of
  up to a few thousand events without dropping.
- **16384 or more** — large per-process memory budget, high peak
  volume. Covers multi-second sink stalls without loss.

Every queued event holds a `LogEvent` reference (~1–2 KB depending on
properties), so a 16384 queue is around 16–32 MB worst case.

#### Step 4. Flush on shutdown

```csharp
// On process shutdown:
logger.Dispose();  // drains the queue before returning
```

`Dispose` lets in-flight events finish. Without it, the process may
exit while events are still in the queue.

---

## What's NEW and IMPROVED in the Herald approach

| Serilog.Sinks.Async | Herald async |
|---|---|
| Wrap each sink you want async | One pipeline-wide switch |
| One drop policy (block or drop) | Two explicit strategies — `DropWrite` and `Wait`, configurable timeout |
| Silent drops | Every drop flows to `ILogFailureSink` with a reason (`QueueFull`, `SyncWaitTimeout`) |
| No easy way to read queue depth | `AsyncLogger.QueueDepth` property exposed via `PipelineAccessor` — healthchecks and dashboards read it directly |
| Async wrapper is opaque | Wrapped sinks keep Herald's zero-allocation contract — per-event cost is the same as a non-async sink |
| Separate bounded buffer per wrapped sink | Single bounded queue per pipeline — one memory bound to reason about |

**Bonus:** `WithAsyncLogging` pairs with `WithDeferredRendering`.
That combination moves the expensive message-template rendering to
the background thread too, not just the sink write. For pipelines
with null sinks (benchmarks, tests) the rendering is skipped
entirely.

---

## Full working example

```csharp
using MMP.Herald.Quick;
using MMP.Herald.Services;

var logger = QuickLogBuilder
    .Create("app")
    .WithMinimumLevel("info")
    .WithFileSink("logs/app.log")
    .WithDatadogSink(apiKey: Environment.GetEnvironmentVariable("DD_API_KEY")!)
    // Async with a generous queue and silent drops — non-blocking.
    .WithAsyncLogging(
        capacity: 8192,
        dropStrategy: KnownDropStrategies.DropWrite)
    .BuildAndCommit()
    .Logger;

// Use the logger normally from any thread.
logger.Info("requests", "request {Id} completed in {Ms} ms", requestId, elapsed);

// On graceful shutdown (drains the queue):
// logger.Dispose();
```

### Game loop example

Games care about per-frame latency. The logging system must not block
the render thread under any circumstance.

```csharp
var logger = QuickLogBuilder
    .Create("game")
    .WithMinimumLevel("info")
    .WithFileSink("logs/game.log")
    // Large queue + drop-oldest — a buffer full means losing the oldest
    // log, not blocking the current frame.
    .WithAsyncLogging(
        capacity: 16384,
        dropStrategy: KnownDropStrategies.DropOldest)
    .BuildAndCommit()
    .Logger;
```

---

## Troubleshooting

**My logs are disappearing.**
The queue filled up and the drop strategy kicked in. Check
`ILogFailureSink` for `QueueFull` reasons. Fix by one of: raising
`capacity`, making the sink faster, or switching to `Wait` if you
can't afford to drop.

**My application hangs for 100 ms on log calls.**
You're on `Wait` and the queue filled. Either raise capacity or
switch to `DropWrite` if latency matters more than completeness.

**On shutdown I'm losing the last few events.**
You need to `Dispose` the logger before the process exits. In an
ASP.NET Core app, the host does this for you if the pipeline is
registered in the DI container; in a console app, call `Dispose`
manually in a `finally` block.

**How do I know the queue is backing up?**
`PipelineAccessor.Get<AsyncLogger>()` exposes the current queue
depth. Emit it as a metric or surface it on your `/health` endpoint.
A rising queue depth is a leading indicator that a sink is slower
than the event rate.

---

## See also

- `Modules/Core/docs/under-the-hood.md` — full pipeline tour
- `Modules/Core/native/dotnet/Pipeline/AsyncLogger.cs` — the implementation
- `KnownDropStrategies` — available drop policies
- `QuickLogBuilder.WithAsyncLogging()` — the pipeline step
- `QuickLogBuilder.WithDeferredRendering()` — defers template rendering to the worker thread
