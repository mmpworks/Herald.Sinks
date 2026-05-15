# Herald.Sinks.Bugsnag

> Reports Herald log events to Bugsnag via the public notify API. Drop-in for Serilog.Sinks.Bugsnag. HTTP-only implementation — no Bugsnag.NET SDK dependency.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.Bugsnag
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `bugsnag` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- HTTP POST per event to notify.bugsnag.com (Payload v5)
- Severity mapping (error/critical/security -> error, warn -> warning, else info)
- Category becomes errorClass; message + template + level land in metaData.herald
- On-prem Bugsnag URL override via the endpoint constructor parameter

## Limitations

- One HTTP request per event — pair with WithAsyncLogging for high volume
- No stack-trace handling (Bugsnag's own SDK extracts those from Exception); use the code-first ctor and a custom payload if you need them
- No session tracking — out of scope for a logging sink

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: HttpClient is thread-safe per BCL contract.

## Vendor

SmartBear / Bugsnag — https://www.bugsnag.com

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
