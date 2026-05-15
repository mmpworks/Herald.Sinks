@../../CODING_INSTRUCTIONS.md

When running Python commands, use `python` instead of `python3` (Windows).

# Scope

Herald.Sinks is the canonical home for every Herald log-sink implementation
outside Core's built-ins (console, null, file, HTTP/TCP/UDP). Every
destination-specific sink ships here as an independently-versioned NuGet
package.

The repo serves two audiences:

1. **Herald release pipeline.** The official Herald metapackages
   (`MMP.Herald`, `MMP.Herald.Business`, etc.) pull in curated subsets
   of these packages. Individual sinks can be upgraded without churning
   Core.

2. **Community contributions.** A new destination belongs here, not in
   Core. The CONTRIBUTING guide spells out the contract: an
   `ILogSinkProvider` implementation, tests, and a `CAPABILITY.yaml`
   manifest.

# Build

- All sinks + all tests: `bash build.sh`
- Release + tests: `bash build.sh --release --test`
- Individual sink: `dotnet build src/Herald.Sinks.<Name>/`

# Layout

```
Herald.Sinks/
├── Herald.Sinks.sln               # all sinks + tests as one solution
├── build.sh                       # builds + tests every sink
├── CAPABILITY-SCHEMA.md           # the schema every sink's CAPABILITY.yaml follows
├── tools/
│   └── product-sheet.py           # reads CAPABILITY.yaml files, emits catalog
├── src/
│   └── Herald.Sinks.<Name>/
│       ├── Herald.Sinks.<Name>.csproj   # publishes Herald.Sinks.<Name>
│       ├── CAPABILITY.yaml              # capability manifest
│       ├── <Name>LogSink.cs
│       └── Providers/<Name>LogSinkProvider.cs
└── tests/
    └── Herald.Sinks.<Name>.Tests/
        ├── Herald.Sinks.<Name>.Tests.csproj
        └── <Name>LogSinkTests.cs
```

# Capability manifests

Every sink ships a `CAPABILITY.yaml` at its root. The `tools/product-sheet.py`
script reads these across the repo and produces the authoritative product
catalog. Never rely on README disclaimers for capability — the manifest is
the contract.

# References

- `CAPABILITY-SCHEMA.md` — manifest schema
- `CONTRIBUTING.md` — how to add a new sink
- `../Core/Herald.Core.csproj` — the sink contract (ILogSinkProvider,
  LogEvent, etc.) consumers depend on
