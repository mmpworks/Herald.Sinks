# Herald.Sinks.HttpJson

> Posts Herald log events as newline-delimited JSON (NDJSON) over HTTP. One event per line, batched when the pipeline supplies multiple. Pairs with any HTTP log-intake that accepts NDJSON — Elasticsearch bulk, Loki push, generic aggregators, custom backends.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.HttpJson
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `http_json` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Batched delivery via IBatchedLogSink
- Async path (LogAsync / LogBatchAsync) for the AsyncLogger drain
- Shared HttpClient pool via internal SocketsHttpHandler — reuses TCP connections across requests
- 30-second default timeout on the owned HttpClient
- Caller-supplied HttpClient honored verbatim — timeout, handler, and disposal stay with the caller

## Limitations

- No gzip compression on the request body
- No built-in retry — pair with the async decorator for retry policy
- Relies on the remote endpoint accepting NDJSON; no framing negotiation

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — shared HttpClient, both sync Send and async SendAsync paths.

## Vendor

Herald — https://github.com/mmpworks/Herald.Sinks

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
