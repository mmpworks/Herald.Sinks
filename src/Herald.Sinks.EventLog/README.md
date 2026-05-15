# Herald.Sinks.EventLog

> Writes Herald log events to the Windows Event Log. Drop-in for Serilog.Sinks.EventLog — forwards level / category / message through EventLog.WriteEntry with a mapped EventLogEntryType. Windows-only; throws PlatformNotSupportedException on Linux and macOS.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.EventLog
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `event_log` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Maps Herald level to EventLogEntryType (Info / Warning / Error)
- Truncates at Event Log's 31839-character message ceiling
- [SupportedOSPlatform("windows")] compiler-enforced platform marker
- Autocreate-on-first-write available via code-first ctor flag

## Limitations

- Windows-only — throws PlatformNotSupportedException elsewhere
- Source must be pre-registered (admin-only operation)
- Message ceiling is 31839 chars — longer events truncate silently
- Synchronous Send — wrap with async decorator for heavy volume

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — EventLog.WriteEntry is internally synchronized.

## Vendor

Microsoft — https://learn.microsoft.com/dotnet/api/system.diagnostics.eventlog

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
