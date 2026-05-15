# Herald.Sinks.AmazonS3

> Uploads Herald log events as NDJSON objects to an Amazon S3 bucket. Drop-in for Serilog.Sinks.AmazonS3. One batch → one S3 object. Pairs naturally with Herald.Sinks.AwsCloudWatch for archive vs live-query separation.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.AmazonS3
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `aws_s3` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Date-partitioned key layout (yyyy-MM-dd) for listable buckets
- NDJSON body — streams well into Athena / Redshift Spectrum / S3 Select
- Default AWS credential chain
- Code-first overload accepts a pre-built IAmazonS3

## Limitations

- One S3 object per batch — batch sizing drives object count
- No multipart upload in 1.0 — batches must fit in memory
- Synchronous Send via .GetAwaiter().GetResult()
- No SSE-C / KMS key parameters surfaced yet — follow-up feature

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — AWS SDK client is thread-safe.

## Vendor

Amazon Web Services — https://docs.aws.amazon.com/AmazonS3/

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
