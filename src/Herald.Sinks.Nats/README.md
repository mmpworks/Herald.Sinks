# Herald.Sinks.Nats

> Publishes Herald log events as JSON messages to a NATS subject via NATS.Client.Core (the modern v2 line). Cloud-native messaging with optional JetStream durability handled at the operator side.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.Nats
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `nats` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- PublishAsync per event
- Subjects support hierarchical routing (herald.logs.error, herald.logs.audit)
- Code-first overload accepts a pre-built NatsConnection for shared client scenarios

## Limitations

- Synchronous Log path uses GetAwaiter().GetResult around the async SDK
- JetStream durability is operator responsibility — wire JetStreamContext on the consumer side
- No TLS / NKey auth helpers — pass a pre-configured NatsConnection via the code-first ctor

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — NatsConnection is thread-safe per SDK contract.

## Vendor

NATS.io / Synadia — https://nats.io

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
