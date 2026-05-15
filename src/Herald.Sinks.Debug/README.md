# Herald.Sinks.Debug

> Writes Herald log events to System.Diagnostics.Debug. Events appear in Visual Studio / Rider / VS Code Output windows with a debugger attached, and in any registered DebugListener otherwise. Intended for development-time diagnostic output without touching stdout.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.Debug
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `debug` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Zero external dependencies — BCL only
- Survives Release builds (DEBUG defined in csproj)
- Optional category prefix for IDE Output-window filtering
- Exception text appended after the formatted line

## Limitations

- Single fixed line format — no custom template surface
- No structured emission — properties are not rendered separately
- Output visibility depends on a registered DebugListener or attached debugger

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — Debug.WriteLine is internally synchronized.

## Vendor

Microsoft — https://learn.microsoft.com/dotnet/api/system.diagnostics.debug

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
