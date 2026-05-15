# Herald.Sinks.ClickHouse

> Inserts Herald log events into a ClickHouse table via the ClickHouse.Client driver. Single INSERT on the Log path, ClickHouseBulkCopy on IBatchedLogSink batches — the driver's native column-block format gives sustained-throughput insert performance ClickHouse is famous for.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.ClickHouse
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `clickhouse` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Single INSERT per event on the Log path
- ClickHouseBulkCopy on batches — column-block native format
- Identifier validation prevents SQL injection on the table name
- LowCardinality columns suggested in the schema for level + category

## Limitations

- Synchronous Log path uses GetAwaiter().GetResult around the async bulk-copy SDK
- Schema creation is operator responsibility
- No automatic Properties column wiring; add a Map(String, String) column and bind via custom subclass

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Connection per call; no shared state across calls.

## Vendor

ClickHouse — https://clickhouse.com

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
