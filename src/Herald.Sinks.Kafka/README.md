# Herald.Sinks.Kafka

> Produces Herald log events as JSON messages to a Kafka topic via Confluent.Kafka. Drop-in for Serilog.Sinks.Kafka. Default config (acks=all, linger.ms=5, compression=snappy) gives durable, network-efficient delivery without further tuning.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.Kafka
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `kafka` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Produce per event with internal batching via linger.ms (5 ms default)
- Snappy compression on by default — bandwidth-efficient out of the box
- Acks=all for durable replication-aware delivery
- Optional partition key derivation via Func<LogEvent, string?> for ordered consumption
- Code-first overload accepts a pre-built IProducer for shared client scenarios
- Final Flush on Dispose so in-flight messages aren't lost on shutdown

## Limitations

- No transactional producer support (idempotent commits across topics) — out of scope for a logging sink
- SASL / TLS auth is left to ProducerConfig — pass a custom config via the code-first overload
- Producer queue is bounded by Confluent.Kafka's queue.buffering.max.messages (default 100k)

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — Confluent.Kafka producers are thread-safe per SDK contract.

## Vendor

Apache Kafka / Confluent — https://kafka.apache.org

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
