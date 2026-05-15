# Herald.Sinks.AzureServiceBus

> SendMessage Herald log events into an Azure Service Bus queue or topic via Azure.Messaging.ServiceBus. Single SendMessage on the Log path, SendMessages on batches.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.AzureServiceBus
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `azure_service_bus` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- SendMessage per event with ContentType=application/json
- Subject = level.key for cheap subscription rules on topics
- SendMessages on IBatchedLogSink batches (single AMQP transfer per batch)
- Code-first overload accepts a pre-built ServiceBusSender for shared client scenarios

## Limitations

- Synchronous Log path uses GetAwaiter().GetResult around the async SDK
- 1 MB max message size (Premium tier 100 MB) — operator owns oversize policy

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — ServiceBusSender is thread-safe per Azure SDK contract.

## Vendor

Microsoft Azure — https://learn.microsoft.com/azure/service-bus-messaging/

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
