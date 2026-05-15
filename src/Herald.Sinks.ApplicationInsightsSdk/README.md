# Herald.Sinks.ApplicationInsightsSdk

> Tracks Herald log events as TelemetryClient.TrackTrace and TrackException into Azure Application Insights via the Microsoft.ApplicationInsights SDK. Drop-in for Serilog.Sinks.ApplicationInsights. Events carrying an exception in their context promote to ExceptionTelemetry so AI's exception analytics light up. Pair with Herald.Sinks.ApplicationInsightsHttp if you want the same destination without the SDK dependency.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.ApplicationInsightsSdk
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `application_insights_sdk` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- TraceTelemetry for normal events with SeverityLevel mapped from level key
- ExceptionTelemetry for events with $exception in their context
- Properties bag carries category, template, and event properties
- Code-first overload accepts a pre-built TelemetryClient for shared DI scenarios
- Final Flush on Dispose so in-flight telemetry survives shutdown

## Limitations

- Connection-string ctor builds a private TelemetryConfiguration; for shared DI use the code-first overload
- Properties are stringified — non-string structured values lose shape
- No sampling-rate override; configure on the TelemetryClient via DI
- Pulls Microsoft.ApplicationInsights as a transitive dependency; use Herald.Sinks.ApplicationInsightsHttp for a narrower dependency graph

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — TelemetryClient is thread-safe per Microsoft SDK contract.

## Vendor

Microsoft Azure — https://learn.microsoft.com/azure/azure-monitor/app/app-insights-overview

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
