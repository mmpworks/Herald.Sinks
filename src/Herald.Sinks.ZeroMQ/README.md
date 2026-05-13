# MMP.Herald.Sinks.ZeroMQ

> Publishes Herald log events as JSON frames over a ZeroMQ socket via NetMQ. Drop-in for Serilog.Sinks.ZeroMQ. Supports PUB (fan-out to subscribers) and PUSH (load-balance to pullers) socket kinds. Pair with downstream SUB or PULL consumers for ad-hoc log streaming where a full broker would be overkill.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package MMP.Herald.Sinks.ZeroMQ
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `zeromq` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- PUB socket for fan-out to multiple subscribers (with topic prefix)
- PUSH socket for load-balanced work-queue delivery
- Single-threaded poller drains a thread-safe NetMQQueue — producers Log() from any thread
- JSON payload via Utf8JsonWriter (AOT clean)
- Bind defaults match canonical patterns (PUB binds, PUSH connects); override per-instance

## Limitations

- PUB drops events for slow / disconnected subscribers (ZeroMQ HWM behaviour)
- PUSH blocks the queue when no puller is connected; pair with WithAsyncLogging for safety
- No TLS / CURVE auth (left to NetMQ's lower-level API)
- First Log() call waits for the poller thread to bind/connect before enqueuing

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — NetMQQueue is the documented thread-safe entry; the socket lives entirely on the poller thread.

## Vendor

ZeroMQ / NetMQ — https://zeromq.org

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
