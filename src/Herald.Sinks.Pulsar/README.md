# Herald.Sinks.Pulsar

> Sends Herald log events to an Apache Pulsar topic via DotPulsar. Multi-tenant pub/sub with persistent and non-persistent topic flavours selected by the topic URL.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.Pulsar
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `pulsar` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Send per event with byte[] schema
- Code-first overload accepts a pre-built IProducer<byte[]>
- Persistent and non-persistent topics selected via the topic URL prefix

## Limitations

- Synchronous Log path uses GetAwaiter().GetResult around the async DotPulsar API
- No partitioned-topic key-based routing helper — pass a custom producer via the code-first ctor
- No JWT / TLS auth helpers — wire via DotPulsar's ClientBuilder

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — DotPulsar IProducer is thread-safe per SDK contract.

## Vendor

Apache Pulsar — https://pulsar.apache.org

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
