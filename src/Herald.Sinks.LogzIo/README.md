# MMP.Herald.Sinks.LogzIo

> Ships Herald log events to Logz.io's bulk HTTP listener. Drop-in for Serilog.Sinks.Logz.Io. NDJSON body, token carried in the URL.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package MMP.Herald.Sinks.LogzIo
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `logzio` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- NDJSON payload (one event per line) per Logz.io spec
- Token travels in URL; no Authorization header needed
- Regional listener override via listenerUrl
- Batched POST per IBatchedLogSink

## Limitations

- Synchronous Send path
- Regional listeners (EU, AU) require manual URL override

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — shared HttpClient.

## Vendor

Logz.io — https://docs.logz.io/docs/shipping/code/dotnet/serilog

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
