# Herald.Sinks.PostgreSQL

> Writes Herald log events as rows in a PostgreSQL table. Drop-in for Serilog.Sinks.PostgreSQL. Parameterized INSERT for single writes, binary COPY for batches (~10x faster than prepared INSERTs), optional auto-create DDL.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.PostgreSQL
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `postgresql` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Parameterized INSERT for single-event Log calls
- Binary COPY via NpgsqlBinaryImporter for IBatchedLogSink batches
- Properties stored as JSONB for native JSON query (WHERE properties->>'TenantId' = 'acme')
- Custom column-name options via PostgreSQLColumnOptions
- Optional idempotent auto-create DDL
- Connection pooling via Npgsql

## Limitations

- Requires PostgreSQL 12+ for JSONB column type
- No schema migration — pre-existing tables with a narrower schema fail on first write
- No COPY CSV fallback — BinaryImporter only

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — each call opens a pooled Npgsql connection.

## Vendor

PostgreSQL — https://www.postgresql.org

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
