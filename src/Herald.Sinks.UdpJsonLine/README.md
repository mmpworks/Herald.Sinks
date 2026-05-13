# MMP.Herald.Sinks.UdpJsonLine

> Fires Herald log events as newline-delimited JSON datagrams over UDP. One datagram per event, no acknowledgement, no retry. Fits syslog-style collectors and lossy-tolerant pipelines where drop-tolerance is acceptable and latency matters more than durability.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package MMP.Herald.Sinks.UdpJsonLine
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `udp_json_line` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Fire-and-forget datagram delivery — minimal per-event cost
- Async path (LogAsync / LogBatchAsync) for the AsyncLogger drain
- Lazy DNS resolution (doesn't block construction on unreachable host)
- Per-event 65,000-byte hard ceiling with helpful error on oversized events

## Limitations

- No delivery guarantee — events can be silently dropped by the network
- No retry, no acknowledgement, no ordering guarantee
- Plaintext only — no built-in DTLS
- Per-event size capped (not suited for large property bags or dumps)

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — UdpClient.Send / SendAsync are reentrant.

## Vendor

Herald — https://github.com/smuchow1962/Herald.Sinks

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
