# MMP.Herald.Sinks.MySQL

> Writes Herald log events as rows in a MySQL or MariaDB table via MySqlConnector (the modern, fully-async, open-source driver). Drop-in for Serilog.Sinks.MySQL. Completes the relational-DB trio alongside MSSqlServer and PostgreSQL.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package MMP.Herald.Sinks.MySQL
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `mysql` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Parameterized INSERT on single writes
- Transaction-wrapped batched INSERT (~10x faster than per-row commits)
- Works on MySQL 5.7+ and MariaDB 10.x
- Optional idempotent auto-create DDL
- utf8mb4 charset on auto-create

## Limitations

- Properties stored as TEXT JSON by default; switch to JSON type in the installer for MySQL 5.7+
- No batch-insert multi-row syntax in 1.0 (one INSERT per event under a single transaction)
- Synchronous Send

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — each call opens a pooled MySqlConnection.

## Vendor

Oracle / MariaDB Foundation — https://dev.mysql.com/doc/

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
