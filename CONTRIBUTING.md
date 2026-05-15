# Contributing a sink to Herald.Sinks

This guide walks through adding a new destination-specific sink. Follow the shape and your sink ships as `Herald.Sinks.<Name>` on NuGet with no extra wiring.

## License + the DCO

Herald.Sinks ships under the **Apache License, Version 2.0** — see [`LICENSE`](LICENSE) and [`NOTICE`](NOTICE). Every contribution you submit is licensed under those same terms.

Contributions are gated by the **Developer Certificate of Origin (DCO)**, version 1.1. The canonical text lives in [`DCO`](DCO) at the repository root. Practically:

- Every commit in your pull request must carry a `Signed-off-by:` trailer.
- You add the trailer by passing `-s` to `git commit`:

  ```bash
  git commit -s -m "feat(sink): add MyDestination"
  # message ends with:
  # Signed-off-by: Your Name <your.email@example.com>
  ```

- The email on the sign-off must match the email on the commit author.

The DCO is what the contributor certifies — that you have the right to submit the contribution under Apache 2.0, that the work is yours or you have permission to submit it. It's lighter weight than a Contributor License Agreement and matches the pattern most open ecosystems use for high-volume contribution surfaces. No external signature store, no PAT, no one-time enrollment — just `git commit -s` on every commit.

A GitHub Action runs on every pull request and fails the check if any commit in the PR lacks the sign-off, or if the sign-off email doesn't match the author email. Fix is straightforward:

```bash
# Re-sign the last commit:
git commit --amend -s --no-edit
git push --force-with-lease

# Re-sign every commit on the branch:
git rebase -i main --exec "git commit --amend --no-edit -s"
git push --force-with-lease
```

If you're contributing on behalf of an employer that has IP claims on your work, make sure you have permission to submit under Apache 2.0 before signing.

## The contract

A sink is two things:

1. **An `ILogSinkProvider` implementation** — registers the sink in the pipeline's provider registry. Consumer code calls `registry.Register(new YourLogSinkProvider())` at bootstrap.
2. **An `ILogger` implementation** — receives `LogEvent` instances and ships them to the destination.

Nothing more. No events, no channels, no background threads (unless your destination's wire format genuinely requires one — see the Kafka sink for an example).

## Start from a template

Every existing sink in `src/` is a valid template. Pick one whose shape matches your destination:

- **HTTP POST with JSON body + single auth header** (most common) — copy `Herald.Sinks.Seq/` or `Herald.Sinks.Splunk/`.
- **HTTP POST with stream labels + nanosecond timestamps** — copy `Herald.Sinks.Loki/`.
- **HTTP POST with a custom envelope** (Datadog, Sentry) — copy `Herald.Sinks.Datadog/` or `Herald.Sinks.Sentry/`.
- **Non-HTTP (Kafka, etc.)** — copy `Herald.Sinks.Kafka/` for the client-library pattern.

## Every .cs file ships this header

Every C# source file in this repo opens with the same two-line license header. Copy it verbatim at the top of every new `.cs` file.

```csharp
// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable
```

`Directory.Build.props` at the repo root already sets csproj-level copyright, authors, and `PackageLicenseExpression: Apache-2.0` so every NuGet package inherits the terms. The per-file header is the human-readable surface — readers see the license without opening another file.

## File layout

```
src/Herald.Sinks.<Name>/
├── Herald.Sinks.<Name>.csproj
├── CAPABILITY.yaml
├── <Name>LogSink.cs
├── Providers/<Name>LogSinkProvider.cs
└── README.md                       # optional — only if the sink has
                                    # operator-facing complexity
                                    # CAPABILITY.yaml doesn't cover

tests/Herald.Sinks.<Name>.Tests/
├── Herald.Sinks.<Name>.Tests.csproj
└── <Name>LogSinkTests.cs
```

## The csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Herald.Sinks.<Name></RootNamespace>
    <AssemblyName>Herald.Sinks.<Name></AssemblyName>
    <PackageId>Herald.Sinks.<Name></PackageId>
    <Version>1.0.0</Version>
    <Description>(filled from CAPABILITY.yaml at pack time)</Description>
    <IsAotCompatible>true</IsAotCompatible>   <!-- adjust if the sink
                                                    pulls a reflection-heavy dep -->
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\Core\Herald.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="CAPABILITY.yaml" Pack="true" PackagePath="\" />
  </ItemGroup>
</Project>
```

## The CAPABILITY.yaml

See `CAPABILITY-SCHEMA.md` for the full schema and field-by-field rules. The manifest is non-negotiable — the monorepo build fails if it's missing or incomplete.

Minimum viable manifest for a community-contributed HTTP sink:

```yaml
name: Herald.Sinks.Acme
package_id: Herald.Sinks.Acme
version: 1.0.0
kind: sink
category: observability

purpose: >
  Posts log events to Acme's HTTP log intake.

vendor:
  name: Acme Observability
  url: https://acme.example.com

ships:
  - AcmeLogSink
  - AcmeLogSinkProvider

requires:
  core_version: ">=1.0.0"
  external:
    - Acme API key

config:
  kind: acme
  uri: "https://ingest.acme.example.com/v1/logs"
  host: null
  alias: "<API key>"
  notes: Alias carries the X-Acme-Key header.

capabilities:
  - HTTP POST with JSON body
  - Level mapping to Acme's severity scale

limitations:
  - No batching today
  - No compression

minimum_edition: Community
aot_compatible: true
thread_safety: thread-safe, shared HttpClient
test_coverage: tests/Herald.Sinks.Acme.Tests/AcmeLogSinkTests.cs (8 tests)
product_pack: []
maintenance:
  level: active
  owner: <GitHub handle>
  last_audit: 2026-04-24
changelog:
  - version: 1.0.0
    date: 2026-04-24
    summary: Initial release
```

## The sink implementation

The contract — fully stated:

```csharp
public interface ILogger
{
    void Log(LogEvent logEvent);

    // Override when the destination supports real async I/O.
    // Default forwards to Log(); acceptable for most HTTP sinks.
    ValueTask LogAsync(LogEvent logEvent, CancellationToken cancellationToken = default);
}

public interface ILogSinkProvider
{
    string SinkKind { get; }
    HeraldEdition MinimumEdition { get; }
    ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry);
}
```

Everything else — batching, retries, circuit-breaking, WAL — happens at the pipeline layer. Your sink receives events one by one (or a batch if you implement `IBatchedLogSink`) and ships them to the destination.

## Tests

Every sink ships unit tests. We don't ship integration tests against real destinations in the monorepo — those live in your own repo if you maintain them. A `TestHttpMessageHandler` (linked from `Herald.Core/tests/Helpers/`) captures the outbound request so you can assert on endpoint, headers, and body shape without a network call.

Minimum coverage we expect:

- **Happy path** — one event, right endpoint, right headers, right body shape
- **Batch path** — multiple events if the sink implements `IBatchedLogSink`
- **Level mapping** — every Herald level maps to the right destination severity (theory test with `[InlineData]`)
- **Exception path** — an event carrying an `Exception` context produces the right payload shape
- **Config argument guards** — missing required config throws at construction
- **Endpoint override / auth variants** — every configuration path the sink exposes

Aim for 8-12 tests per sink. More if the sink has real complexity.

For sinks that take structured config through the management API (`logDirectory`, `rollingLogsEnabled`, custom fields) or ship a `configuration-{kind}.mmpform` form, the [testing guide](testing-guide.md) walks through the three SDK contracts every Herald sink speaks (JSON binding shape, management-API round-trip, per-kind form discovery) plus the end-to-end pattern for proving runtime side-effects survive a hot-swap. The reference test files in `Herald.Sinks.File.Tests` cover all of them and adapt cleanly to other sinks.

## Publishing

The repo builds all sinks on every push. A release job (triggered by a git tag) publishes individual NuGet packages for each sink whose `Version` in CAPABILITY.yaml differs from the most-recently-published version on NuGet.

You bump your sink's version by editing both the csproj `<Version>` and the CAPABILITY.yaml `version` fields. A `changelog` entry is required for every bump.

## What doesn't belong here

- **Core sinks** (console, null, text-file, JSON-file, HTTP/TCP/UDP). Those live in `Herald.Core`. Every consumer gets them for free without a separate package.
- **Cloud archive providers for non-sink destinations** (S3 closed-file, Azure Blob closed-file). Those implement `IArchiveProvider`, not `ILogSinkProvider`. Different interface, different responsibility. *(TBD — we may merge these into this repo as `Herald.Sinks.Aws` and `Herald.Sinks.Azure` in a future pass, but the dependency shape is different and they're handled separately today.)*
- **Engine addons** (management API, query DSL, hot-reload machinery). Those are Herald engine modules, not sinks.

## Getting help

- Open an issue on the Herald main repo (`mmpworks/Herald`) with the `sinks` label.
- For destination-specific API questions (rate limits, field semantics, schema) — ask the destination's vendor first, then file an issue here if Herald's sink shape needs to adapt.

## License

Apache License, Version 2.0. All contributions here accept the repo's license. See `LICENSE` for the full text.
