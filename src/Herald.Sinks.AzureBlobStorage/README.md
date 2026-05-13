# MMP.Herald.Sinks.AzureBlobStorage

> Uploads Herald log events as NDJSON blobs to an Azure Blob Storage container. Drop-in for Serilog.Sinks.AzureBlobStorage. Pairs with App Insights / Log Analytics for cheap retention.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package MMP.Herald.Sinks.AzureBlobStorage
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `azure_blob` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Date-partitioned key layout (yyyy-MM-dd) for listable containers
- NDJSON body — streams into Azure Synapse / Data Lake Analytics queries
- Connection-string or DefaultAzureCredential auth (managed identity, etc.)
- One blob per batch — batch sizing drives object count

## Limitations

- No append-blob mode in 1.0 (block blobs only)
- Synchronous Send via blob.Upload
- No SAS-only auth helper — feed via connection string

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — Azure SDK BlobContainerClient is thread-safe.

## Vendor

Microsoft Azure — https://learn.microsoft.com/azure/storage/blobs/

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
