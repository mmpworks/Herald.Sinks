# Herald.Sinks.HelloWorld

> Test-only Herald sink. Registers under kind "hello_world" with a no-op CreateSink so the plugin loader has a concrete provider to discover, hot-add, and hot-remove. The dashboard renders the dashboard_config block here as a sanity check that the manifest pipeline is end-to-end alive — there are no real connection parameters because the sink does nothing.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.HelloWorld
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `hello_world` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Discoverable through Herald's plugin host
- Embedded CAPABILITY.yaml for dashboard form rendering
- Counts received events for debugger inspection

## Limitations

- Does not emit anywhere — events go to /dev/null
- Not intended for production use
- No durability, no retry, no batching

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — counter increments use Interlocked.

## Vendor

MMP — https://github.com/mmpworks/Herald.Sinks

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
