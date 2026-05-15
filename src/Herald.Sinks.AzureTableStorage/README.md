# Herald.Sinks.AzureTableStorage

> Writes Herald log events as entities into an Azure Table Storage table via Azure.Data.Tables. Cheap high-volume archival destination for Azure-hosted workloads. Configurable partition strategy (UtcDay / UtcHour / UtcMinute / Fixed) trades query locality for write throughput; the default UtcDay matches Serilog.Sinks.AzureTableStorage's shape. Auth via connection string OR DefaultAzureCredential (managed identity) OR a caller-built TableClient.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.AzureTableStorage
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `azure_table_storage` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- Four partition strategies — UtcDay, UtcHour, UtcMinute, Fixed — for tuning the locality/parallelism trade-off
- Inverted-tick row key plus per-sink sequence suffix for deterministic newest-first ordering even under burst load
- Transactional inserts for IBatchedLogSink batches (split per-partition, capped at 100/transaction)
- Endpoint-URL auth via DefaultAzureCredential (managed identity in production, local-dev credentials on workstations) when Uri starts with https://
- Connection-string auth when Uri is the shared-key form
- Caller-built TableClient overload for full auth/SAS/lifecycle control
- Property names sanitised to Table Storage's column-name rules so the same Herald property maps to the same column deterministically
- Properties of simple types (string, number, bool, DateTime, Guid, byte[]) land as native Table columns; everything else stringifies
- Exception context (LogContextKeys.Exception) lands as a dedicated Exception column
- Context values (excluding Exception) merge into the entity alongside event properties
- Auto-creates the table on first use

## Limitations

- Single-partition transactions; cross-day batches issue one transaction per day
- Property values of non-primitive types fall back to ToString()
- Column-name conflicts between properties and context resolve first-write-wins via the seen-set
- No TLS / DAC tweaks beyond the SDK defaults

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — TableClient is thread-safe per Azure SDK contract; the per-sink row sequence counter uses Interlocked.Increment.

## Vendor

Microsoft Azure — https://learn.microsoft.com/azure/storage/tables/

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
