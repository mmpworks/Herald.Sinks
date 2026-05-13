# MMP.Herald.Sinks.RabbitMQ

> Publishes Herald log events as JSON messages onto a RabbitMQ exchange with a caller-supplied routing key. Downstream consumers (Graylog, Logstash, custom pipelines) decide what to do with them. Persistent messages by default for crash-safety on the broker side.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package MMP.Herald.Sinks.RabbitMQ
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `rabbitmq` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Single persistent connection + single channel held for sink lifetime
- Automatic connection recovery via RabbitMQ.Client's AutomaticRecoveryEnabled
- JSON body with ContentType=application/json, Timestamp in basic properties
- Persistent or transient delivery mode
- Thread-safe publishes via per-channel lock

## Limitations

- Sink does not declare exchanges or queues — broker-side setup required
- No per-event routing-key computation in 1.0 — fixed key for the sink's lifetime
- No publisher confirms in 1.0 — follow-up for audit-grade delivery guarantees

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe via per-channel publish lock.

## Vendor

VMware (RabbitMQ) — https://www.rabbitmq.com

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
