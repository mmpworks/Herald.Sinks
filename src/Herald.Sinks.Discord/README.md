# MMP.Herald.Sinks.Discord

> Posts Herald log events to a Discord channel via incoming webhooks. One message per event with severity-aware emoji prefix; content truncated at 1900 chars (under Discord's 2000-char ceiling).

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package MMP.Herald.Sinks.Discord
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `discord` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- HTTP POST per event with severity emoji prefix
- Content truncation at 1900 chars
- Pair with WithMinimumLevel('warn') to avoid spamming the channel

## Limitations

- One message per event — high-volume pipelines should filter to warn+
- No embed support today; extend via custom subclass
- No file attachment for stack traces — Discord webhooks don't support files via this path

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: HttpClient is thread-safe per BCL contract.

## Vendor

Discord — https://discord.com

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
