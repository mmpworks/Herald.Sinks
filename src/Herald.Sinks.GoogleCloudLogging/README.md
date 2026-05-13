# MMP.Herald.Sinks.GoogleCloudLogging

> Writes Herald log events to Google Cloud Logging (formerly Stackdriver) via LoggingServiceV2Client.WriteLogEntries. Drop-in for Serilog.Sinks.GoogleCloudLogging. Maps Herald severity to Cloud Logging's LogSeverity enum so the log viewer colour-codes events consistently with other GCP services.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package MMP.Herald.Sinks.GoogleCloudLogging
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `gcp_logging` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Single and batched WriteLogEntries
- Maps Herald level to Cloud Logging LogSeverity (Debug/Info/Notice/Warning/Error/Critical/Alert)
- Properties emitted as LogEntry labels (1024-char cap per value, auto-truncated)
- Exception details emitted as exception / exception_type labels
- Code-first overload accepts a pre-built LoggingServiceV2Client
- MonitoredResource override for GCE / GKE / Cloud Run workloads

## Limitations

- No JsonPayload path in 1.0 — all properties go on Labels
- MonitoredResource defaults to 'global' (override via code-first ctor)
- Synchronous Send path via .GetAwaiter().GetResult()
- Label values truncate at 1024 chars (Cloud Logging ceiling)

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — LoggingServiceV2Client is thread-safe per GCP SDK contract.

## Vendor

Google Cloud — https://cloud.google.com/logging/docs

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
