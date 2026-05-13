# MMP.Herald.Sinks.OtlpGrpc

> Exports Herald log events to an OpenTelemetry collector over OTLP/gRPC. Calls opentelemetry.proto.collector.logs.v1.LogsService/Export with the standard ExportLogsServiceRequest payload. Reuses the hand-rolled protobuf writer from Herald.Sinks.Otlp so payload shape stays identical across HTTP and gRPC transports — no proto codegen, no generated message types.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package MMP.Herald.Sinks.OtlpGrpc
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `otlp_grpc` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- OTLP/gRPC log export to any compliant collector (OpenTelemetry Collector, Tempo, Honeycomb, Grafana Agent, etc.)
- Reuses OtlpProtobufLogSerializer from Herald.Sinks.Otlp — identical wire payload to the HTTP-OTLP sibling sink
- Raw byte marshallers on the gRPC method descriptor — no proto-codegen step, no generated IMessage types, AOT-clean
- Batched delivery via IBatchedLogSink
- Configurable per-call deadline (default 30s)

## Limitations

- No compression on the request body (matches the HTTP-OTLP sibling)
- No mTLS / custom credentials configurable through runtime-definition today — construct OtlpGrpcLogSink directly with a configured GrpcChannel for advanced auth scenarios
- Resource attributes configurable at construction only

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — GrpcChannel is shared across calls and is itself thread-safe per gRPC contract.

## Vendor

OpenTelemetry — https://opentelemetry.io/docs/specs/otlp/

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
