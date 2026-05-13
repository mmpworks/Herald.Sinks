# MMP.Herald.Sinks.DynamoDB

> PutItems Herald log events into a DynamoDB table via AWSSDK.DynamoDBv2. Drop-in for Serilog.Sinks.AmazonDynamoDB. Composite id sorts newest-first within a date prefix; BatchWriteItem chunks into 25-item slices.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package MMP.Herald.Sinks.DynamoDB
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `dynamodb` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- PutItem per event on the Log path
- BatchWriteItem on IBatchedLogSink batches, chunked at 25 items per request
- Composite id (date#inverted-ticks#rand) for newest-first scans
- Code-first overload accepts a pre-built IAmazonDynamoDB
- Property bag serialised as JSON in the properties attribute

## Limitations

- Synchronous Log path uses GetAwaiter().GetResult around the async SDK
- Item attribute size (and per-item ceiling) bound by DynamoDB's 400 KB limit
- No automatic table creation or schema management — operator responsibility

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — IAmazonDynamoDB clients are thread-safe per AWS SDK contract.

## Vendor

Amazon Web Services — https://aws.amazon.com/dynamodb/

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
