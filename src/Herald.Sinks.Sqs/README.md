# Herald.Sinks.Sqs

> SendMessage Herald log events into an AWS SQS queue via AWSSDK.SQS. Drop-in for Serilog.Sinks.AmazonSqs. Chunks batches at 10 messages per request (the SQS SendMessageBatch limit).

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.Sqs
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `sqs` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- SendMessage per event
- SendMessageBatch for batches, chunked at 10 messages per request
- Code-first overload accepts a pre-built IAmazonSQS

## Limitations

- Synchronous Log path uses GetAwaiter().GetResult around the async SDK
- 256 KB per message — operator owns oversize-event policy
- FIFO queues require a MessageGroupId; not configured by this sink (use code-first to add it)

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — IAmazonSQS is thread-safe per AWS SDK contract.

## Vendor

Amazon Web Services — https://aws.amazon.com/sqs/

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
