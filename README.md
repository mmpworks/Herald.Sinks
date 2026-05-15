# Herald.Sinks

The canonical home for every Herald log-sink implementation outside Core's built-ins.

Herald.Core ships with the universal sinks every consumer needs: console, null, text-file, JSON-file, HTTP, TCP, UDP. Everything else — Splunk, Datadog, Loki, Honeycomb, S3, Azure Blob, Kafka, Sentry, PagerDuty, SignalFx, Seq, OTLP — lives here, each as an independently-versioned NuGet package.

## Who this repo is for

**Herald consumers.** You want a specific destination. Install the matching NuGet:

```bash
dotnet add package Herald.Sinks.Datadog
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `registry.Register(...)` call is required — `dotnet add package` is the whole workflow. Configure the sink either through `herald.json` (`kind: datadog`) or by constructing it directly and attaching it with `.WithBridge(sink)`. See [`docs/adding-sinks.md`](docs/adding-sinks.md) for the full walkthrough.

You can also install a curated pack via one of the official Herald metapackages (`MMP.Herald.Business`, `MMP.Herald.Game.Pro`). See [Herald.OSS](https://github.com/mmpworks/Herald.OSS) for the full metapackage list.

**Contributors.** You want to add a new destination. Read `CONTRIBUTING.md` for the contract. Every sink ships an `ILogSinkProvider`, tests against a fake `HttpMessageHandler` (or equivalent), and a `CAPABILITY.yaml` manifest.

## Catalog

The authoritative catalog — what each sink does, its configuration shape, its dependencies, its maintenance status — is generated from each sink's `CAPABILITY.yaml` file. Run the product-sheet generator to see it:

```bash
python tools/product-sheet.py
```

This produces `docs/product-sheet.md` with one section per sink.

## Layout

```
Herald.Sinks/
├── Herald.Sinks.sln              # solution with every sink and every test
├── build.sh                      # build + test every sink
├── CAPABILITY-SCHEMA.md          # schema every sink's CAPABILITY.yaml follows
├── CONTRIBUTING.md               # how to add a new sink
├── tools/
│   └── product-sheet.py          # reads CAPABILITY.yaml files, emits docs
├── src/
│   └── Herald.Sinks.<Name>/
│       ├── Herald.Sinks.<Name>.csproj
│       ├── CAPABILITY.yaml
│       ├── <Name>LogSink.cs
│       └── Providers/<Name>LogSinkProvider.cs
├── tests/
│   └── Herald.Sinks.<Name>.Tests/
└── docs/
    └── product-sheet.md          # generated catalog
```

## Versioning

Each sink is versioned independently. A bump to `Herald.Sinks.Datadog` does not require a Core release or any other sink release. The sink's csproj carries a `Version` and a `ReleaseNotes` entry; the NuGet package inherits both.

Semver rules:
- **Patch** — bug fix, no wire-format change, no config-key change
- **Minor** — new optional configuration keys, new opt-in capability
- **Major** — wire format change, required-config change, minimum-Core bump

## Relationship to Herald.Core

Every sink here depends on `Herald.Core` — specifically `ILogSinkProvider`, `LogEvent`, `LogLevel`, and related contracts. The dependency flows one way: sinks know Core, Core doesn't know any sink in this repo.

Core stays AOT-clean and reflection-free on the public surface. Each sink declares its own AOT compatibility in `CAPABILITY.yaml`; consumers who publish AOT-compiled binaries consult the manifest to avoid pulling in sinks that break trim.

## License

Apache License, Version 2.0. See [`LICENSE`](LICENSE) for the full text. Copyright held by MMPWorks LLC.
