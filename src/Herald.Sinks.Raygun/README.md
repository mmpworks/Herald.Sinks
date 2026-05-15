# Herald.Sinks.Raygun

> Forwards Herald log events to the Raygun crash-reporting / error- tracking service. Drop-in for Serilog.Sinks.Raygun. Pair with a warn+ level filter — Raygun is sized for crash reports, not info chatter.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.Raygun
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `raygun` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Maps log message → details.error.message and exception → className/stackTrace
- Custom data carries Herald properties + level/category
- Tags include level and category for Raygun filters
- Pure HTTP — no Raygun SDK dependency

## Limitations

- One event per request (Raygun's entries API is per-event)
- Synchronous Send — pair with async decorator + level filter

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — shared HttpClient.

## Vendor

Raygun — https://raygun.com/documentation/

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
