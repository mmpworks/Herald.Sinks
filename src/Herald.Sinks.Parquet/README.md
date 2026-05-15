# Herald.Sinks.Parquet

> Writes Herald log events as Apache Parquet files for long-term analytical storage. One Parquet file per batch, seven-column schema (time_utc, level, category, message, template, exception, properties). Optional Iceberg catalog hook (no-op by default) for future table-format integration.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.Parquet
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `parquet` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Apache Parquet 2.x columnar files via Parquet.Net
- Seven-column v1 schema covering the full LogEvent surface
- JSON-encoded properties column (transitional — MAP<STRING,STRING> is the planned target)
- Iceberg catalog seam (IIcebergCatalogClient) with a no-op default, callable after each file closes

## Limitations

- One Parquet file per LogBatch — naive sizing, no rolling/aggregation yet (planned for v1.x to match Herald.Sinks.File's evolution path)
- Properties stored as stringified JSON, not native MAP type
- No compression configuration knob — Parquet.Net default applies
- Iceberg integration is a stub interface only; no REST-catalog implementation ships in v1.0

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — each LogBatch call writes its own file. Concurrent callers produce concurrent files; filenames are timestamp-keyed with millisecond resolution so collisions require sub-ms calls on the same writer instance.

## Vendor

Apache Parquet — https://parquet.apache.org

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
