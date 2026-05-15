# Herald.Sinks.GodotConsole

> Routes Herald log events to Godot 4.x's built-in console output channels. Levels at or above Error use GD.PushError (Errors tab, audible stinger in the editor); Warn uses GD.PushWarning (Warnings tab); everything below uses GD.Print (Output panel). Game-dev counterpart to Herald.Sinks.Debug — registers as a normal Herald sink via QuickLogBuilder, no embedding required.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.GodotConsole
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `godot_console` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Per-level routing to Godot's three console channels (Output, Warnings, Errors)
- Optional category prefix for filtering
- Exception text appended after the formatted line
- Pure synchronous dispatch — no async overhead

## Limitations

- GD.Print and friends are static — sink behavior depends on a loaded Godot runtime
- Single fixed line format; wrap with an output transformer chain for custom layouts
- Editor Output panel does not render ANSI color codes (text-only)

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — Godot's console APIs are internally synchronized.

## Vendor

Godot Foundation — https://godotengine.org

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
