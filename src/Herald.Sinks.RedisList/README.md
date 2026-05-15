# Herald.Sinks.RedisList

> RPUSHes Herald log events as JSON entries onto a Redis list via StackExchange.Redis. Drop-in for Serilog.Sinks.Redis.List. Pairs with downstream BLPOP / BRPOPLPUSH consumers for durable handoff to a worker tier; pair with the optional LTRIM cap for bounded retention.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.RedisList
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `redis_list` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- RPUSH per event on the Log path
- Single multi-value RPUSH on IBatchedLogSink batches (one round trip)
- Optional LTRIM cap for bounded retention (oldest evicted first)
- Code-first overload accepts a pre-built IDatabase for shared multiplexer scenarios
- JSON payload via Utf8JsonWriter (AOT clean)

## Limitations

- LTRIM runs after every push when MaxLength > 0; large bursts amortize the cost
- Consumer-side acknowledge / retry semantics are the operator's responsibility (use BRPOPLPUSH for at-least-once)
- Property values of non-primitive types fall back to ToString()

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — the StackExchange.Redis multiplexer is thread-safe per driver contract.

## Vendor

Redis — https://redis.io

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
