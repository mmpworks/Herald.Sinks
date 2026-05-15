# Herald.Sinks.AzureCosmosDB

> Writes Herald log events as documents in an Azure Cosmos DB container via the modern Microsoft.Azure.Cosmos v3 SDK. Drop-in for Serilog.Sinks.AzureCosmosDB. Partition-keyed on category by default for query locality.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.AzureCosmosDB
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `azure_cosmosdb` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Document insert via Container.CreateItemAsync
- GUID per-document id
- Partition key from event category (override via code-first ctor)
- Code-first overload accepts a pre-built CosmosClient
- Batching via per-event CreateItemAsync (enable Cosmos bulk mode on the client for higher throughput)

## Limitations

- No transactional batch in 1.0 — each event is its own write
- No per-item TTL in 1.0 — set container TTL at deploy time
- Synchronous Send path via .GetAwaiter().GetResult()
- AAD / Managed Identity auth not surfaced in 1.0; use code-first ctor with a preconfigured CosmosClient

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — CosmosClient is thread-safe per Azure SDK contract.

## Vendor

Microsoft Azure — https://learn.microsoft.com/azure/cosmos-db/

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
