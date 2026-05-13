# MMP.Herald.Sinks.Graylog

> Sends Herald log events as GELF 1.1 messages to Graylog over HTTP or TCP. Drop-in for Serilog.Sinks.Graylog / Graylog.Batching. Common target for European enterprise shops and any Graylog-as-central-log- aggregator deployment.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package MMP.Herald.Sinks.Graylog
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `graylog` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- GELF 1.1 wire format (version, host, short_message, full_message, timestamp, level)
- HTTP and TCP transports (UDP chunking is a follow-up)
- Custom fields prefixed with underscore per GELF spec
- Syslog severity mapping (0-7) from Herald level
- Exception → full_message + _exception_type
- TCP reconnect-on-failure with single retry

## Limitations

- No UDP / chunking in 1.0
- No GZIP compression on the HTTP transport
- Synchronous Send — pair with async decorator for heavy volume

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe. HTTP is stateless on the client side; TCP serialises through a per-sink lock.

## Vendor

Graylog — https://docs.graylog.org/docs/gelf

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
