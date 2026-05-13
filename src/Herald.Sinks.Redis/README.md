# MMP.Herald.Sinks.Redis

> Publishes Herald log events as JSON messages to a Redis PubSub channel via StackExchange.Redis. Drop-in for Serilog.Sinks.Redis (PubSub mode). Pair with Herald.Sinks.RedisList when you need durable delivery (RPUSH into a list consumers drain).

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package MMP.Herald.Sinks.Redis
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `redis` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- PUBLISH per event with FireAndForget for minimum latency
- JSON payload via Utf8JsonWriter (AOT clean)
- Code-first overload accepts a pre-built ISubscriber for shared multiplexer scenarios
- Property values round-trip for common BCL primitives

## Limitations

- PubSub is fire-and-forget — subscribers offline at publish time miss events
- No batched publish (Redis PubSub has no native batching)
- Pattern channels (PSUBSCRIBE) are subscriber-side; the sink uses literal channel names

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
