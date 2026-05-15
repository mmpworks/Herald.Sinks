# Herald.Sinks.GoogleCloudPubSub

> Publish Herald log events into a Google Cloud Pub/Sub topic via Google.Cloud.PubSub.V1. PublisherClient handles internal batching, retries, and flow control by default.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.GoogleCloudPubSub
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `google_pubsub` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- PublishAsync per event; the SDK batches internally based on size and time
- Per-message attributes for level + category enable cheap subscription filters
- ShutdownAsync flushes pending publishes on Dispose
- Code-first overload accepts a pre-built PublisherClient

## Limitations

- Synchronous Log path uses GetAwaiter().GetResult around the async SDK
- Authentication via Application Default Credentials only (env, gcloud, instance)
- 10 MB max message size (per Google's quota)

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — PublisherClient is thread-safe per Google SDK contract.

## Vendor

Google Cloud — https://cloud.google.com/pubsub

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
