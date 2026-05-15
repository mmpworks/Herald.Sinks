# Herald.Sinks.BigQuery

> Streaming-inserts Herald log events into a Google BigQuery table via Google.Cloud.BigQuery.V2. ADC for auth.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.BigQuery
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `bigquery` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- InsertRows per batch — BigQuery's streaming insert API
- Code-first overload accepts a pre-built BigQueryClient
- Application Default Credentials chain for auth

## Limitations

- 1 MB per row, 10 MB per request — operator owns sizing
- Streaming inserts cost per-row; pair with WithAsyncLogging + batch sizing

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: BigQueryClient is thread-safe per Google SDK contract.

## Vendor

Google Cloud — https://cloud.google.com/bigquery

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
