# MMP.Herald.Sinks.Datadog

> Posts Herald log events to Datadog's HTTP log intake. Works against both the public site intake and a local Datadog Agent, with per-event error-triple emission that lights up Datadog's error-tracking pipeline in addition to plain log search.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package MMP.Herald.Sinks.Datadog
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `datadog` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Batched delivery via IBatchedLogSink
- Level mapping to Datadog status (trace→debug, fatal→emergency)
- Exception triple (error.message / error.kind / error.stack)
- Static tags merge with per-event category into ddtags
- Works direct-to-Datadog or via a local Datadog Agent

## Limitations

- No compression today (gzip support is a future enhancement)
- Static tags only via construction; JSON config limited to URI / service / key
- Synchronous Send path; pair with the async decorator for throughput

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — shared HttpClient, synchronous Send.

## Vendor

Datadog — https://docs.datadoghq.com/api/latest/logs/

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
