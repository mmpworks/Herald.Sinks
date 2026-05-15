# Herald.Sinks.InfluxDB

> Writes Herald log events to InfluxDB v2 via line protocol over HTTP. No InfluxDB SDK dependency. Tags carry level + category; message rides as a field. Code-first only.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.InfluxDB
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `influxdb` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- HTTP POST per batch as line protocol
- Token auth (Authorization Token <token>)
- Tags level + category for cardinality-aware indexing
- ms-precision timestamps

## Limitations

- Provider throws — credentials + bucket must be wired via code-first
- No retention or downsampling helpers (operator responsibility)

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: HttpClient is thread-safe per BCL contract.

## Vendor

InfluxData — https://www.influxdata.com

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
