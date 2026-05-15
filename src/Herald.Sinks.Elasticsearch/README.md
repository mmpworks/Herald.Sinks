# Herald.Sinks.Elasticsearch

> Sends Herald log events to Elasticsearch as JSON documents via the Bulk API. Uses time-based indexes (prefix-yyyy.MM.dd) and emits exceptions as a typed sub-object (type / message / stack) that Kibana's exception-tracking UI indexes as first-class fields.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.Elasticsearch
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `elasticsearch` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Batched delivery via the Bulk API (one NDJSON request per batch)
- Time-based indices ({prefix}-yyyy.MM.dd) for easy retention policies
- Exception emission as typed sub-object (type / message / stack)
- Separate properties / context namespaces guard against user-property collisions with reserved fields
- Index-prefix regex validation at construction

## Limitations

- No auth support yet (API key / basic auth requires caller-supplied HttpClient)
- Index prefix configurable at construction only
- Synchronous Send path; pair with async decorator for throughput

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — shared HttpClient is reentrant.

## Vendor

Elastic — https://www.elastic.co/guide/en/elasticsearch/reference/current/docs-bulk.html

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
