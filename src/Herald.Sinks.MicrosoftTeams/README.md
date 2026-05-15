# Herald.Sinks.MicrosoftTeams

> Posts Herald log events to a Microsoft Teams channel via an incoming webhook. Drop-in for Serilog.Sinks.MicrosoftTeams.Alternative. MessageCard format — compatible with both Office 365 Connectors and the newer Workflows webhook.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.MicrosoftTeams
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `ms_teams` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- MessageCard format with colour-coded themeColor
- Facts table auto-populated from properties
- Exception type surfaced as a fact
- Title override for branded alerts

## Limitations

- Teams rate limits at ~1 req/sec; pair with a warn+ level filter
- No batching — one event per webhook call (Teams cards are per-event)
- MessageCard format is officially "retired" but still works; Adaptive Card follow-up planned

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — shared HttpClient.

## Vendor

Microsoft — https://learn.microsoft.com/microsoftteams/platform/webhooks-and-connectors/how-to/add-incoming-webhook

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
