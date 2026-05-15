# Herald.Sinks.GenericWebhook

> Generic HTTP webhook sink with optional rules engine. POSTs formatted log events to any endpoint. The rules engine supports cooldown, message-pattern matching, property/category conditions, and per-rule payload templates — the feature set that PagerDuty, Opsgenie, and Datadog use for incident routing.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.GenericWebhook
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `webhook` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Batched delivery via IBatchedLogSink when rules are absent
- Rules engine (ordered, first-match-wins with continue-on-match)
- Per-rule cooldown (seconds) to prevent flooding
- Conditions: MinLevel, CategoryEquals/Contains, MessageContains/Matches, PropertyExists, PropertyEquals
- Per-rule custom payload templates with placeholders ({level}, {category}, {message}, {timestamp}, {ruleName}, {prop:Name})
- Regex-cache for MessageMatches patterns, 100ms pattern timeout
- Header-injection protection on caller-supplied headers

## Limitations

- Rules / headers / formatter configurable at construction only
- JSON config path does not surface the rules engine — use GenericWebhookSinkRegistration.RegisterWithRules
- Content type configurable at construction only

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — HttpClient is reentrant; rule-engine cooldowns under ConcurrentDictionary.

## Vendor

Herald — https://github.com/mmpworks/Herald.Sinks

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
