# MMP.Herald.Sinks.Syslog

> Emits Herald log events as RFC 5424 or RFC 3164 syslog messages over UDP or TCP. Drop-in for Serilog.Sinks.SyslogMessages. Cross-platform — works on Windows, Linux, and macOS. Typical targets: rsyslog, syslog-ng, Graylog, Logstash, Fluentd with a syslog input.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package MMP.Herald.Sinks.Syslog
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `syslog` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- RFC 5424 and RFC 3164 wire formats
- UDP and TCP transports (TCP framing via RFC 6587 octet counting)
- Facility selector covering all 24 RFC 5424 slots
- Cross-platform — no native deps, pure BCL sockets
- Reconnect-on-failure for TCP with single retry per event

## Limitations

- No TLS transport in 1.0 — follow-up planned
- Structured-data field (RFC 5424) currently emitted as NILVALUE
- Synchronous Send — pair with async decorator for high volume
- No message batching on TCP — one frame per event

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe. UDP writes are stateless on the client side; TCP writes serialize through a per-sink lock so frames don't interleave.

## Vendor

IETF — https://www.rfc-editor.org/rfc/rfc5424

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
