# Herald.Sinks.Telegram

> Sends Herald log events to a Telegram chat via the Bot API. HTTP-only; no Telegram.Bot SDK dependency.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.Telegram
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `telegram` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- HTTP POST per event to api.telegram.org/bot{token}/sendMessage
- Text truncation at 4000 chars (Telegram hard cap is 4096)
- Pair with WithMinimumLevel('warn') to avoid spam

## Limitations

- One message per event; no batching support in Telegram's Bot API
- No Markdown / HTML formatting today; extend via custom subclass
- Bots can only message users / channels they've been added to

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: HttpClient is thread-safe per BCL contract.

## Vendor

Telegram — https://core.telegram.org/bots/api

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
