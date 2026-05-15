# Herald.Sinks.AwsCloudWatch

> Writes Herald log events to an AWS CloudWatch Logs log group / log stream via PutLogEvents. Drop-in for Serilog.Sinks.AwsCloudWatch. Honours CloudWatch's 10,000-event / 1 MB batch ceiling and the 256 KB single-event ceiling. Optional auto-create of log group and stream.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.AwsCloudWatch
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `aws_cloudwatch` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Single and batched PutLogEvents (respects 10,000-event / 1 MB / 256 KB ceilings)
- Stable-sort by timestamp to satisfy CloudWatch's chronological-order requirement
- JSON-formatted message bodies for CloudWatch Logs Insights queries
- Default AWS credential chain (env, shared creds, IMDS, SSO, role)
- Code-first overload accepts a pre-built IAmazonCloudWatchLogs client

## Limitations

- No retention-policy management — configure via IaC or installer
- Oversize events truncate silently rather than fail the batch
- Synchronous Send path (.GetAwaiter().GetResult() around async AWS SDK calls)

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — the AWS SDK client is thread-safe per AWS SDK contract.

## Vendor

Amazon Web Services — https://docs.aws.amazon.com/AmazonCloudWatch/latest/logs/

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
