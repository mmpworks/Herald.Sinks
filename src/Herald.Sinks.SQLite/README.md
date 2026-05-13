# MMP.Herald.Sinks.SQLite

> Writes Herald log events to an embedded SQLite database. Drop-in for Serilog.Sinks.SQLite. Single-file, no server, no background process. Ideal for desktop apps, CLI tools, and edge deployments.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package MMP.Herald.Sinks.SQLite
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `sqlite` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Auto-creates table if missing (CREATE TABLE IF NOT EXISTS)
- Batched INSERT wrapped in a transaction (~10x faster than per-row commits)
- Per-sink lock serialises writes (SQLite semantics)
- Properties stored as JSON TEXT column
- Table-name identifier validation (letter/digit/underscore only)

## Limitations

- SQLite serialises writers — not suitable for high-concurrency services
- No WAL-mode setup helper in 1.0 — set via connection string / PRAGMA
- Synchronous Send (SQLite has no async path worth using)

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe via per-sink lock. Single connection held for lifetime.

## Vendor

SQLite / Microsoft — https://learn.microsoft.com/dotnet/standard/data/sqlite/

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
