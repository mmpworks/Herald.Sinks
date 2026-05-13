# MMP.Herald.Sinks.Dynatrace

> Posts Herald log events to Dynatrace Generic Log Ingest API via HTTP with Api-Token authentication. Drop-in for Serilog.Sinks.Dynatrace. No Dynatrace SDK dependency.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package MMP.Herald.Sinks.Dynatrace
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `dynatrace` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Batched POST per IBatchedLogSink
- Severity mapping (DEBUG/INFO/WARN/ERROR/CRITICAL)
- Pure HTTP — no Dynatrace SDK

## Limitations

- 5 MB / 50,000-entry request ceiling enforced by Dynatrace
- Synchronous Send path — pair with async decorator

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — shared HttpClient.

## Vendor

Dynatrace — https://docs.dynatrace.com/docs/discover-dynatrace/references/dynatrace-api/environment-api/log-monitoring/post-ingest-logs

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
