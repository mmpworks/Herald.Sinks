# Herald.Sinks.SignalFx

> Posts Herald log events to SignalFx / Splunk Observability Cloud's HTTP log intake. Routes by realm or against a self-hosted Splunk Observability endpoint, with optional low-cardinality dimensions applied to every event for metric correlation.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.SignalFx
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `signalfx` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Batched delivery via IBatchedLogSink
- Realm-based default endpoint with full-URL override for self-hosted
- Low-cardinality dimensions emitted on every event for metric correlation
- Per-event properties land at payload root; exceptions expand to exception / exception.type
- Reserved-field collision guard (seen-set prevents property-over-reserved overwrites)

## Limitations

- No gzip compression on the request body
- Dimensions configured at construction only — not surfaced through JSON config
- Synchronous Send path; pair with the async decorator for high-throughput pipelines

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — shared HttpClient, synchronous Send.

## Vendor

Splunk — https://docs.splunk.com/observability/en/admin/logs/logs.html

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
