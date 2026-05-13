# MMP.Herald.Sinks.ApplicationInsightsHttp

> POSTs Herald log events as MessageData telemetry envelopes directly to Azure Application Insights' ingestion endpoint. No Microsoft.ApplicationInsights SDK dependency — just HttpClient. Narrow dependency graph, AOT-friendly. Same destination as Herald.Sinks.ApplicationInsightsSdk, different implementation approach. Pick this for minimal dependencies or AOT-clean publishing; pick the SDK variant for idiomatic AI telemetry types (TraceTelemetry, ExceptionTelemetry) and Serilog drop-in semantics.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package MMP.Herald.Sinks.ApplicationInsightsHttp
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `application_insights_http` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Direct HTTP POST of MessageData envelopes; no SDK round-trip overhead
- Connection-string parser supports modern AI format AND legacy bare-instrumentation-key
- SeverityNumber → AI SeverityLevel mapping
- Optional HttpClient injection for pooling / test mocking
- Implements IBatchedLogSink — pipeline batching decorator packs N events per request
- Cloud role name (ai.cloud.role) populated from the sink Alias when set

## Limitations

- Properties are stringified — non-string structured values lose shape
- No sampling-rate override; configure batch sizes upstream
- No automatic dependency tracking, performance counters, or other AI SDK features beyond log telemetry
- Bypasses Microsoft.ApplicationInsights.Channel buffering — failed POSTs do not auto-retry on the SDK channel

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — HttpClient operations are concurrent-safe; the sink owns no mutable shared state.

## Vendor

Microsoft Azure — https://learn.microsoft.com/azure/azure-monitor/app/app-insights-overview

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
