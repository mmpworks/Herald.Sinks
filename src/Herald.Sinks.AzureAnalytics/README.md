# Herald.Sinks.AzureAnalytics

> Writes Herald log events to Azure Log Analytics / Azure Monitor via the HTTP Data Collector API. Drop-in for Serilog.Sinks.AzureAnalytics. Pure HTTP — no Azure SDK dependency. HMAC-SHA256 signing per Data Collector API spec.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.AzureAnalytics
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `azure_analytics` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Data Collector API with HMAC-SHA256 request signing
- No Azure SDK dependency — pure BCL HTTP + cryptography
- Properties flattened to prop_<Name> columns for Log Analytics query compatibility
- Batched POST per IBatchedLogSink
- time-generated-field header set to event timestamp for correct Kusto time filtering

## Limitations

- Data Collector is Azure's legacy ingest surface; Log Ingestion API is the modern path (follow-up feature)
- No structured jsonPayload support — properties flatten to columns
- Key rotation requires sink reconstruction
- Synchronous Send — pair with async decorator for heavy volume

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — HttpClient and HMACSHA256 signing are thread-safe.

## Vendor

Microsoft Azure — https://learn.microsoft.com/azure/azure-monitor/logs/data-collector-api

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
