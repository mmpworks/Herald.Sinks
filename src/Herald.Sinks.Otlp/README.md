# MMP.Herald.Sinks.Otlp

> Three sinks for the OpenTelemetry Logs Protocol that share serialization infrastructure: OtlpJsonLogSink posts JSON to an OTLP collector, OtlpProtobufLogSink posts binary protobuf to the same endpoint, and ProtobufFileLogSink writes length-delimited protobuf records to a local .pb file for offline ingestion. The hand-rolled protobuf writer keeps the package AOT-clean — no generated message classes, no reflection paths the trim analyzer has to defeat.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package MMP.Herald.Sinks.Otlp
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `otlp_json | otlp_protobuf | protobuf_file` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Three sinks in one package — pick network (JSON or protobuf) or file
- Hand-rolled protobuf writer — AOT-clean, no generated IMessage types
- OTLP semantic conventions (severityNumber, severityText, body, attributes, resource, traceId, spanId)
- Batched delivery via IBatchedLogSink for the network sinks
- Length-delimited protobuf framing for the file sink (readable by streaming readers)
- Rolling-by-size for the file sink (max file size, automatic sequential rollover)
- Hex-nibble trace/span id parsing with no exception path (perf-aware)

## Limitations

- No gRPC transport — HTTP/1.1 only. gRPC-OTLP is a future enhancement.
- Resource attributes configurable at construction only
- No compression on the request body

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Network sinks thread-safe via shared HttpClient. File sink serializes writes under a private lock so single-writer and concurrent-writer workloads both behave.

## Vendor

OpenTelemetry — https://opentelemetry.io/docs/specs/otlp/

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
