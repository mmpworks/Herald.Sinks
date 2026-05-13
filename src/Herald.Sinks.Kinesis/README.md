# MMP.Herald.Sinks.Kinesis

> PutRecords Herald log events into an AWS Kinesis Data Stream via AWSSDK.Kinesis. Drop-in for Serilog.Sinks.AmazonKinesis. Defaults to category-based partition keys for shard affinity; chunks batches at 500 records per request.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package MMP.Herald.Sinks.Kinesis
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `kinesis` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- PutRecord per event on the Log path
- PutRecords for batches, chunked at 500 records per request (Kinesis limit)
- Default partition key is event category for shard affinity
- Optional Func<LogEvent, string> partition-key accessor
- Code-first overload accepts a pre-built IAmazonKinesis

## Limitations

- Synchronous Log path uses GetAwaiter().GetResult around the async SDK
- 1 MB per record / 5 MB per PutRecords call — operator owns capacity planning
- No automatic stream creation

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — IAmazonKinesis is thread-safe per AWS SDK contract.

## Vendor

Amazon Web Services — https://aws.amazon.com/kinesis/

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
