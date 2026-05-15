# Herald.Sinks.PagerDuty

> Posts Herald log events to PagerDuty's Events API v2 for incident creation. Designed for high-severity alerting, not general log forwarding — stack behind a Warn-or-above filter so routine info events don't page the on-call rotation.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.PagerDuty
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `pagerduty` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Events API v2 trigger payloads
- Stable dedup-key derivation (event id → template → message hash)
- Severity mapping to PagerDuty's four-value scale
- Per-event custom_details bag with exception + properties + context
- 1024-char defensive summary cap
- Caller-supplied dedup resolver for per-event deduplication

## Limitations

- Only emits "trigger" actions — no ack / resolve workflow
- Single-event POST (no batching)
- Component / group / dedup resolver configurable at construction only

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — shared HttpClient, synchronous Send.

## Vendor

PagerDuty — https://developer.pagerduty.com/api-reference/368ae3d938c9e-send-an-event

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
