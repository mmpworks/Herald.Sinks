# Herald.Sinks.UnityConsole

> Routes Herald log events to Unity's console — Debug.Log, Debug.LogWarning, Debug.LogError. Takes delegates in the constructor so the package itself has no UnityEngine reference; the consumer wires UnityEngine.Debug methods directly. Result: the package is fully trim-clean and IL2CPP / AOT-publish-safe, and the consumer keeps full control over how Unity dispatch is wired (real Debug for editor / standalone, custom logger for tests, no-op for headless CI).

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.UnityConsole
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `unity_console` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Per-level routing to Unity's three Debug channels (Log, LogWarning, LogError)
- Zero compile-time dependency on UnityEngine — pure NuGet, IL2CPP / AOT publish-safe
- Optional category prefix for filtering
- Consumer-controlled dispatch (test mocks, no-op routes, custom Unity wrappers all work)

## Limitations

- The consumer must wire Debug.Log / LogWarning / LogError (or equivalent) at construction. The sink does not auto-discover UnityEngine.
- Single fixed line format; wrap with an output transformer chain for custom layouts
- Unity Console does not render ANSI color codes (text-only)

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — the sink itself holds only immutable delegate references. Whether the wired UnityEngine.Debug methods are thread-safe is a Unity-runtime question (they are, on Editor and Player; check the Unity docs for older versions).

## Vendor

Unity Technologies — https://unity.com

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
