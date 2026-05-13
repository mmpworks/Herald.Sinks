# MMP.Herald.Sinks.RavenDB

> Stores Herald log events as documents in a RavenDB database via the official RavenDB.Client SDK. Drop-in for Serilog.Sinks.RavenDB. Per-call session for Log; batched session for IBatchedLogSink.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package MMP.Herald.Sinks.RavenDB
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `ravendb` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- One session per Log; batched session for IBatchedLogSink
- Document model with Level / Category / Message / Template / Properties
- Code-first overload accepts a pre-initialised IDocumentStore for shared scenarios
- Property bag stored as native Dictionary for RQL queries

## Limitations

- No TLS / certificate construction helpers — use the code-first overload for HSM scenarios
- Property values are passed through to RavenDB's BSON layer; complex BCL types may not round-trip
- No automatic database creation — operator responsibility

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — IDocumentStore is thread-safe per client contract; sessions are per-call.

## Vendor

Hibernating Rhinos / RavenDB — https://ravendb.net

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
