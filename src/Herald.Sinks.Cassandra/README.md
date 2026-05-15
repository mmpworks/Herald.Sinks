# Herald.Sinks.Cassandra

> INSERTs Herald log events into a Cassandra table via the DataStax CassandraCSharpDriver. Drop-in for Serilog.Sinks.Cassandra. Works against ScyllaDB and any Cassandra-protocol cluster. Prepared statement bound per event keeps per-call cost bounded.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.Cassandra
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `cassandra` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Prepared INSERT bound per event (one round trip per Log call)
- Per-event Execute on batches; let the session pool handle concurrency
- Code-first overload accepts a pre-connected ISession
- Daily partition + DESC clustering produces newest-first scans by default
- Property bag serialised as JSON in the properties text column

## Limitations

- Schema creation is operator responsibility — the sink expects the table to exist
- No logged-batch INSERTs (Cassandra anti-pattern across partitions); operator can bind a custom batch via code-first
- Property values of non-primitive types fall back to ToString()

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: no
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — ISession is thread-safe per DataStax driver contract.

## Vendor

Apache Cassandra / DataStax / ScyllaDB — https://cassandra.apache.org

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
