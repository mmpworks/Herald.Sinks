# Herald.Sinks.Aliyun

> Sends Herald log events to Alibaba Cloud Simple Log Service (SLS) via the REST PutLogs API. Primary target for China-region deployments and any workload that reports into the Aliyun log ecosystem. Pure HTTP with inline HMAC-SHA1 signing — no Aliyun SDK dependency.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.Aliyun
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `aliyun_sls` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- HMAC-SHA1 request signing per Aliyun SLS spec (API version 0.6.0)
- JSON body with __logs__ array and __time__ timestamp fields
- STS / role auth bypass via empty credentials + custom HttpClient
- Batched POST per IBatchedLogSink
- Pure HTTP — no Aliyun SDK transitive dependency

## Limitations

- No protobuf wire format in 1.0 (JSON only)
- No topic / source fields surfaced through JSON config
- Synchronous Send path
- Key rotation requires sink reconstruction

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — shared HttpClient and stateless signing.

## Vendor

Alibaba Cloud — https://help.aliyun.com/product/28958.html

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
