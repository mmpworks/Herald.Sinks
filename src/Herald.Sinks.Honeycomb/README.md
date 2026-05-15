# Herald.Sinks.Honeycomb

> Posts Herald log events to Honeycomb's batch ingest endpoint. Maps each event onto Honeycomb's flat data-object shape with per-event sample-rate support so aggregates stay correct when an upstream sampler is in play.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.Honeycomb
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `honeycomb` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Batched delivery via IBatchedLogSink
- Per-event samplerate for upstream-sampled pipelines
- Full level / category / message / messageTemplate on every event
- Exception emission as exception + exception.type flat fields
- Reserved-field collision guard

## Limitations

- Flat data shape only — nested properties stringified
- Synchronous Send path; pair with async decorator for throughput
- Sample rate configurable at construction, not through JSON config

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — shared HttpClient, synchronous Send.

## Vendor

Honeycomb — https://docs.honeycomb.io/api/tag/Events

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
