# Herald.Sinks.Rollbar

> Reports Herald log events to Rollbar via the public Items API. HTTP-only implementation — no Rollbar SDK dependency.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.Rollbar
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `rollbar` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- HTTP POST per event to api.rollbar.com/api/1/item
- Level mapping (trace/debug -> debug, info/notice -> info, warn -> warning, error -> error, critical/security -> critical)
- Environment label rides the data.environment field for cross-env grouping
- On-prem Rollbar URL override via the endpoint constructor parameter

## Limitations

- One HTTP request per event — pair with WithAsyncLogging for high volume
- No stack-trace handling — out of scope for a logging sink
- No person/user tracking — extend the body via a custom subclass

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: HttpClient is thread-safe per BCL contract.

## Vendor

Rollbar — https://rollbar.com

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
