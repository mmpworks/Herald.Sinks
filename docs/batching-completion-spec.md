# Herald.Sinks.Batching — completion spec
# Date: 2026-06-01  Author: Steve / Richard / Jared

## What's already done (DO NOT redo)
- `src/Herald.Sinks.Batching/BatchingLogSinkDecorator.cs` — exists, correct
- `src/Herald.Sinks.Batching/BatchingOptions.cs` — exists, correct
- `src/Herald.Sinks.Batching/Herald.Sinks.Batching.csproj` — exists
- 61 providers already call `BatchingLogSinkDecorator.Wrap(...)` — do not rewire them
- `tests/Herald.Sinks.Batching.Tests/` — exists, 13/13 green

## Task A — BatchingNetworkSinkBase (new file in Herald.Sinks.Batching)

`src/Herald.Sinks.Batching/BatchingNetworkSinkBase.cs`

```csharp
// Default IBatchedLogSink for network sinks without a native batch API.
// LogBatch loops over events and calls Log(e). Sinks with a native batch
// endpoint override LogBatch with an efficient implementation.
public abstract class BatchingNetworkSinkBase : HeraldSinkBase, IBatchedLogSink
{
    public virtual void LogBatch(IReadOnlyList<LogEvent> events)
    {
        foreach (var e in events) Log(e);
    }
}
```

## Task B — Add IBatchedLogSink to 18 non-batched network sinks

These 18 sink CLASSES need their base changed from `HeraldSinkBase` to `BatchingNetworkSinkBase`:

Bugsnag, Discord, Graylog, MicrosoftTeams, Mqtt, Nats, PagerDuty, Pulsar,
RabbitMQ, Raygun, Redis, Rollbar, Sentry, Slack (SlackWebhookSink or similar),
Syslog, Telegram, Twilio, ZeroMQ

Find each: `src/Herald.Sinks.<Name>/<Name>LogSink.cs` (or similar).
Change: `: HeraldSinkBase` → `: BatchingNetworkSinkBase`

Also add `ProjectReference` to Herald.Sinks.Batching in each sink's csproj
(same as the already-wired sinks).

## Task C — Wire the 18 providers + ProtobufFile

The 18 providers above are NOT yet wired. Wire them exactly like the 61 already-wired ones:
1. Add `using MMP.Herald.Sinks.Batching;`
2. In CreateSink: `var sink = new XxxLogSink(...); return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));`

ProtobufFile: its sink ALREADY implements IBatchedLogSink. Just wire its provider
(`src/Herald.Sinks.Otlp/Providers/ProtobufFileLogSinkProvider.cs`) the same way.

## Task D — BatchingSinkProviderBase (new file in Herald.Sinks.Batching)

`src/Herald.Sinks.Batching/BatchingSinkProviderBase.cs`

```csharp
// Abstract base for any ILogSinkProvider whose sink is IBatchedLogSink.
// Overrides GetFormSchemaText() to append the three batching config fields
// so the Dashboard shows batchSize/flushIntervalMs/queueCapacity on every
// batched sink's form — without editing any individual mmpform file.
public abstract class BatchingSinkProviderBase : ILogSinkProvider
{
    public abstract ILogger CreateSink(LoggingRuntimeSinkDefinition definition);
    public abstract string SinkKind { get; }
    // ... other ILogSinkProvider members delegated to abstract/virtual

    public virtual string? GetFormSchemaText(string sinkKind)
        => BatchingFormAppender.Append(GetBaseFormSchemaText(sinkKind));

    // Subclass calls base's embedded-resource read.
    protected virtual string? GetBaseFormSchemaText(string sinkKind) => null;
}
```

Read `ILogSinkProvider.cs` in Herald.OSS for the EXACT interface members.
`BatchingSinkProviderBase` must implement every one (abstractly or with defaults).

## Task E — BatchingFormAppender (new file in Herald.Sinks.Batching)

The mmpform DSL format (from configuration-loki.mmpform):

```
# comment
columns: 12
__properties = [
    "field_name" = { type: "string", default: "" },
]
tooltips = [ "tt-x" = "..." ]
[container("Title", "Subtitle")]
  - [widget(cols,{field},...)] Label
```

`BatchingFormAppender.Append(string? existing)` must:
1. If existing is null/empty: return a minimal schema with just the three batching fields
2. Otherwise: splice the three fields into the existing `__properties` block
   (find `__properties = [`, find its closing `]`, insert before the `]`)
   Then append three render rows at the end of the last container.
3. Must be idempotent — if the three fields are already present, do not duplicate them.

The three fields to add:
```
"batch_size"        = { type: "int", default: "256" },
"flush_interval_ms" = { type: "int", default: "1000" },
"queue_capacity"    = { type: "int", default: "8192" }
```

Render rows (append inside the last container, before its closing or at end of file):
```
  - [number(12,{batch_size})] Batch size
  - [number(12,{flush_interval_ms})] Flush interval (ms)
  - [number(12,{queue_capacity})] Queue capacity
```

Keep the parser minimal: text-search for `__properties = [` and its closing `]`.
No full DSL parser needed.

## Task F — Switch ALL wired providers to BatchingSinkProviderBase

Every provider (the 61 already-wired + the 18 + ProtobufFile = ~80 total) should:
- Change from `sealed class XxxProvider : ILogSinkProvider` to `class XxxProvider : BatchingSinkProviderBase`
- Replace the embedded-resource `GetFormSchemaText` call (if any) with an override of `GetBaseFormSchemaText` that calls the base resource read
- The base class `GetFormSchemaText` will then auto-append the batching fields

Check how existing providers implement `GetFormSchemaText` first (grep for it in a wired provider).
If they use a default interface method, the override is a one-liner.

## Task G — Fix level-name drift in Herald.Sinks tests + source

Herald.OSS 0.12.0 renamed:
- `KnownLogLevels.Info` → `KnownLogLevels.Information`
- `KnownLogLevels.Warn` → `KnownLogLevels.Warning`

Also check for `KnownLogLevelKeys.Warn`, `KnownLogLevelKeys.Critical` — grep Herald.OSS
`src/Levels/` to get the authoritative current names, then replace_all throughout
`E:/dev/herald/Modules/Herald.Sinks/src/` and `tests/`.

## Final step — build and commit

```bash
cd E:/dev/herald/Modules/Herald.Sinks && bash build.sh --test
```

All tests must be green. Then ONE commit:

```
feat(sinks): BatchingLogSinkDecorator — fleet-wide batched async delivery for network sinks

Foundation fix for the 250 kHz Loki soak stall: every network sink now batches
by default (256 events / 1 s flush / 8192-event channel) instead of one-POST-per-event.

Herald.Sinks.Batching package:
  BatchingLogSinkDecorator  — bounded Channel<LogEvent>, single drain, size-or-time
    flush, LogBatch on drain thread, per-event failure reporting, drain-on-dispose
  BatchingOptions            — reads batchSize/flushIntervalMs/queueCapacity from config
  BatchingNetworkSinkBase   — default IBatchedLogSink loop for non-native-batch sinks
  BatchingSinkProviderBase  — injects batch config fields into every sink's Dashboard form
  BatchingFormAppender       — DSL-aware __properties splice, no per-mmpform edits

79 network sinks wired (61 existing + 18 newly IBatchedLogSink + ProtobufFile).
Level-name drift fixed: KnownLogLevels.Info→Information, Warn→Warning.
CUPID/DRY: one decorator, one base class, one word change per provider/sink.
```

Do NOT push.
