# Herald.Sinks.File

> Writes Herald log events to disk. Plain-text mode produces human-readable lines; structured mode produces NDJSON for machine parsing. Optional file rolling for time-based or size-based rotation, with per-pipeline path templating.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.File
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `file` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Plain-text or NDJSON output via the format toggle
- Time-based rolling (hour / day / week)
- Size-based rolling (maxBytes per file)
- Retention by file count or total size cap
- Path templating with {pipeline}, {date}, {hour} tokens

## Limitations

- Local filesystem only (use a remote sink for cloud storage)
- One file per sink instance — fan-out via multiple sink registrations

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe via internal lock around write+flush.

## Vendor

MMP

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
