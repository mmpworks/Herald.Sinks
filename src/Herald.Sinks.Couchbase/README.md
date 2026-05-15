# Herald.Sinks.Couchbase

> Upserts Herald log events as JSON documents into a Couchbase bucket / scope / collection via CouchbaseNetClient. Drop-in for Serilog.Sinks.Couchbase. Composite document key sorts newest-first within a date prefix; N1QL queries land on Level / Category / TimeUtc fields.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.Couchbase
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `couchbase` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Upsert per event on the Log path
- Concurrent UpsertAsync fan-out for IBatchedLogSink batches
- Composite key (date#inverted-ticks#rand) for newest-first scans
- Code-first overload accepts a pre-built ICouchbaseCollection

## Limitations

- Synchronous Log path uses GetAwaiter().GetResult around the async SDK
- No native bulk upsert (the SDK exposes per-doc UpsertAsync only)
- Provider throws — wire credentials via the code-first ctor

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: no
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — Cluster + Collection instances are thread-safe per Couchbase SDK contract.

## Vendor

Couchbase — https://www.couchbase.com

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
