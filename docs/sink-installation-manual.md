# Sink Installation Manual

End-to-end procedure for installing a Herald sink package from NuGet and
wiring it into a Herald.OSS pipeline. Read this when you want a
step-by-step checklist with a verification gate at every stage.

A more narrative version of the same material lives at
[`adding-sinks.md`](adding-sinks.md). Read that first if you want the
mental model; come here when you want the procedure.

---

## Contents

- **Part 1** — Prerequisites
- **Part 2** — Choose a sink
- **Part 3** — Install the package
- **Part 4** — Auto-registration: what `dotnet add package` already did
- **Part 5** — Wire the sink (fluent path)
- **Part 6** — Wire the sink (JSON config path)
- **Part 7** — Wrap network sinks in async — mandatory for production
- **Part 8** — Production wiring
- **Part 9** — Verify
- **Part 10** — Troubleshoot
- **Part 11** — AOT publishing
- **Appendix A** — Sink catalog by category
- **Appendix B** — Cheat sheet

---

## Part 1 — Prerequisites

### 1.1 Toolchain

You need the .NET SDK installed and an existing or new project. Herald
sinks target **net8.0**, **net9.0**, and **net10.0**.

**Verify:**

```bash
dotnet --list-sdks
```

You should see at least one SDK in the 8.x / 9.x / 10.x line.

### 1.2 Project targets

The sink's target framework moniker must match (or be newer than) one
of the sink's published TFMs. A new console app from
`dotnet new console` defaults to your latest SDK's TFM and works without
edits.

**Verify:** open `<YourApp>.csproj` and confirm `<TargetFramework>`
shows `net8.0`, `net9.0`, or `net10.0` (or `<TargetFrameworks>` listing
one of those).

### 1.3 Herald.OSS reference

Herald sinks depend on **Herald.OSS** — the open-source pipeline core.
If your project doesn't already reference it, install it now. If you
install a Herald.Sinks.X package first, Herald.OSS comes in as a
transitive dependency anyway; this step is for completeness.

```bash
dotnet add package Herald.OSS
```

**Verify:**

```bash
dotnet list package | grep Herald.OSS
```

The output shows `Herald.OSS` at the version published on nuget.org.

---

## Part 2 — Choose a sink

### 2.1 Pick the destination

Decide where the logs need to land. Common cases:

- **Cloud log search** — Datadog, Splunk, Loki, Honeycomb, New Relic,
  Sumo Logic, Logz.io, Mezmo, Elasticsearch, OpenSearch.
- **Cloud archive** — Amazon S3, Azure Blob Storage, Google Cloud Logging.
- **Message bus / stream** — Kafka, Kinesis, Pulsar, NATS, RabbitMQ,
  Azure EventHub / Service Bus.
- **Database** — PostgreSQL, MySQL, MSSqlServer, MongoDB, ClickHouse,
  CouchDB, DynamoDB, Cassandra.
- **Alerting** — PagerDuty, Slack, Discord, MicrosoftTeams, Telegram.
- **Error tracking** — Sentry, Rollbar, Bugsnag, Raygun, ElmahIo,
  Exceptionless, Airbrake.
- **Local / dev** — File, TextWriter, EventLog, Trace, Debug, XUnit.

Appendix A has the full list grouped by category.

### 2.2 Read the sink's manifest

Every sink ships a `CAPABILITY.yaml` at the .nupkg root. Open the
NuGet page (or unzip the package) and read these fields before
installing:

| Field | What to check |
|---|---|
| `minimum_edition` | `Community` means free and Apache 2.0. `Pro` or `Enterprise` require a license key. |
| `aot_compatible` | `true` if your build does `PublishAot=true`. Pulls a hard line — see Part 11. |
| `requires.external` | Things you need to have in hand before the first log line lands (API key, reachable endpoint, etc.). |
| `config` | The fields the sink expects: `uri`, `host`, `alias`, plus any sink-specific keys. |
| `limitations` | Read these — they're honest. Known gaps surface here. |

### 2.3 Verify the sink is current

Cross-check the package's `maintenance.level` field. `active` is fine.
`migration-guide` means the package is a documentation stub for a sink
that no longer ships — skip it.

---

## Part 3 — Install the package

### 3.1 Install

Replace `<Name>` with your chosen sink's PascalCase identifier (e.g.
`Datadog`, `Splunk`, `Kafka`).

```bash
dotnet add package Herald.Sinks.<Name>
```

### 3.2 What just got pulled in

The install brings down:

- `Herald.Sinks.<Name>.dll` — the sink itself.
- `Herald.OSS` — the pipeline core, if not already referenced.
- Vendor SDK dependencies if any. Datadog uses raw `System.Net.Http`;
  AWS sinks pull `AWSSDK.<Service>`; Azure sinks pull `Azure.<Service>`;
  Kafka pulls `Confluent.Kafka` (with the native `librdkafka` redist,
  ~5 MB per platform).

The vendor SDK pulls land transitively. You don't need to install them
separately.

### 3.3 Verify

```bash
dotnet list package | grep Herald
```

Expected:

```
> Herald.OSS                  <version>  <version>
> Herald.Sinks.<Name>         <version>  <version>
```

If your sink pulls a heavy vendor SDK, `dotnet list package --include-transitive`
shows the full graph.

---

## Part 4 — Auto-registration: what `dotnet add package` already did

### 4.1 The mechanism

Every Herald sink assembly carries a `[ModuleInitializer]` emitted by
Herald.OSS's source generator. The initializer fires on assembly load
and registers the sink's provider into
`LogSinkProviderRegistry.Default`. The trigger is any reference to a
type from the sink package — including a `using` directive at the top
of a file that does nothing else with the sink.

That means: **after `dotnet add package`, the sink is already
registered.** No `RegisterAll(...)` call, no
`WithDatadogSinkProvider()` builder extension, no manual wire-up.

### 4.2 Verify the kind is in the registry

Add a one-line check after `BuildAndCommit()` to confirm. This is
useful once during initial setup; remove after.

```csharp
using MMP.Herald.Routing;

System.Console.WriteLine(
    "datadog registered: " +
    LogSinkProviderRegistry.Default.Contains("datadog"));
```

Expected output: `datadog registered: True`.

If `False`, see Troubleshoot 10.2.

### 4.3 The escape hatch

If you want to be explicit (for example, in a unit test that needs
deterministic provider state), every sink ships a
`<Name>SinkRegistration` helper:

```csharp
Herald.Sinks.Datadog.DatadogSinkRegistration.RegisterAll(
    LogSinkProviderRegistry.Default);
```

`RegisterAll` is idempotent. Calling it after auto-registration is a
no-op.

---

## Part 5 — Wire the sink (fluent path)

### 5.1 When to use this path

- The sink's connection details are constants or come from your code,
  not a config file.
- You don't need an operator to change the sink at runtime without a
  redeploy.
- Tests, prototypes, and CLI tools.

If your config lives in a JSON file the operator edits, skip to Part 6.

### 5.2 Construct the sink

Every sink ships a public ctor taking the destination's connection
details. Example for Datadog:

```csharp
using Herald.Sinks.Datadog;

var datadog = new DatadogLogSink(
    apiKey: System.Environment.GetEnvironmentVariable("DD_API_KEY")!,
    service: "my-service",
    intakeUrl: "https://http-intake.logs.datadoghq.com");
```

The exact ctor parameters vary by sink. The sink's per-class
xmldoc names every parameter; the README shows a minimal example.

### 5.3 Build the pipeline

```csharp
using MMP.Herald.Events;
using MMP.Herald.Quick;

var herald = QuickLogBuilder.Create("my-service")
    .WithMinimumLevel("info")
    .WithBridge(datadog)
    .BuildAndCommit();

var log = herald.Logger;
```

`WithBridge` is the universal escape hatch: any `ILogger` plugs in
through it. The Datadog sink is an `ILogger`; so is every other Herald
sink.

### 5.4 First log line

```csharp
log.Info(new LogCategory("App"), "hello from Herald");
```

### 5.5 Verify

Open the destination's UI (Datadog Logs Explorer in this case) and
look for the event. Filter by `service:my-service`.

If nothing arrives, skip to Part 10.

---

## Part 6 — Wire the sink (JSON config path)

### 6.1 When to use this path

- The sink's connection details belong in a config file the operator
  edits.
- You want hot reload — operator changes the file, pipeline rebuilds,
  no app restart.
- Multiple deployments share the same code but ship different
  `herald.json` files.

### 6.2 Author `herald.json`

Drop a `herald.json` at your app's content root (or wherever you
prefer):

```json
{
  "pipelineName": "my-service",
  "minimumLevel": "info",
  "sinks": [
    {
      "kind": "datadog",
      "uri": "https://http-intake.logs.datadoghq.com",
      "host": "my-service",
      "alias": "${DD_API_KEY}"
    }
  ]
}
```

Field meanings:

- `kind` — matches the sink provider's `SinkKind` constant. See
  the sink's `CAPABILITY.yaml` `config.kind` field for the literal.
- `uri` — destination endpoint. Sink-specific.
- `host` — for many HTTP sinks this is the service name. Sink-specific.
- `alias` — for most sinks this is the API key or auth token.
  Sink-specific.

Some sinks accept additional keys in a `properties: { … }` sub-object.
The sink's `CAPABILITY.yaml` lists them.

### 6.3 Build the pipeline

```csharp
using MMP.Herald.Quick;

var herald = QuickLogBuilder.Create()
    .WithJsonConfig("herald.json")
    .WithHotReload()             // optional but recommended
    .BuildAndCommit();

var log = herald.Logger;
```

`WithJsonConfig` reads the file at build time. `WithHotReload` puts a
file watcher on it; an edit triggers a pipeline rebuild on the same
process.

### 6.4 First log line

Same as Part 5.4. Same verify in Part 5.5.

### 6.5 Secret handling

Don't put secrets in `herald.json` if the file ships with your
deployment. Two options:

1. **Environment-variable interpolation.** Use `${ENV_VAR}` in
   `herald.json`; Herald expands it at load time.
2. **External secret store.** Read the secret from your secret manager
   (AWS Secrets Manager, Azure Key Vault, etc.) at startup, build the
   sink with the fluent path (Part 5), and skip the config-file
   indirection for the credential.

---

## Part 7 — Wrap network sinks in async — mandatory for production

### 7.1 Why

Sinks that do network or disk I/O block the calling thread until the
write completes. A synchronous Datadog POST takes anywhere from a few
milliseconds to several seconds depending on latency and load. Your
application thread waits for it.

Herald.OSS detects this case at pipeline-build time and surfaces an
advisory through `KernelDiagnostic.Advisories` if you wire a network
sink without `WithAsync()`. The advisory is informational — the
pipeline still runs — but the warning is correct.

### 7.2 Identify whether your sink is network-bound

The sink class implements `INetworkSink` if it does network or disk
I/O at delivery. Every sink in these categories carries it:

- HTTP / TCP / UDP / gRPC sinks (Datadog, Splunk, Loki, OTLP, …)
- Cloud-service SDK sinks (AWS, Azure, GCP)
- Database sinks (PostgreSQL, MongoDB, Cassandra, …)
- Message-bus sinks (Kafka, RabbitMQ, NATS, …)
- File sinks (rolling files do fsync at boundaries)

In-process sinks (Console, InMemory, Debug, Trace, EventLog,
TextWriter, GodotConsole, UnityConsole, XUnit, HelloWorld) do not
carry it; they don't need `WithAsync`.

### 7.3 Wrap with `WithAsync`

```csharp
var herald = QuickLogBuilder.Create("my-service")
    .WithMinimumLevel("info")
    .WithAsync()                          // <-- add this
    .WithBridge(datadog)
    .BuildAndCommit();
```

`WithAsync` puts a bounded queue between your application thread and
the sink. Producers enqueue and return immediately; a dedicated worker
drains to the sink on its own thread.

### 7.4 Configure the drop strategy

```csharp
.WithAsync(
    queueCapacity: 10_000,
    onFull: AsyncStrategy.DropWrite,      // or AsyncStrategy.Wait
    syncWaitTimeout: TimeSpan.FromSeconds(1))
```

- `DropWrite` — drop events when the queue is full. Use when log
  throughput is unbounded and dropping is preferable to backpressure.
- `Wait` — block the producer when the queue is full, up to
  `syncWaitTimeout`. Use when every event matters and the application
  can tolerate occasional latency spikes.

Drops flow into `ILogFailureSink` with a reason
(`QueueFull`, `SyncWaitTimeout`). Wire one up if you want to surface
drops in your monitoring.

---

## Part 8 — Production wiring

### 8.1 ASP.NET Core / Worker Service

Register the pipeline as a singleton and dispose it on host shutdown.
The pipeline owns the sinks; disposing flushes them.

```csharp
// Program.cs (ASP.NET Core)
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<QuickLogResult>(_ =>
    QuickLogBuilder.Create("my-service")
        .WithJsonConfig("herald.json")
        .WithHotReload()
        .WithAsync()
        .BuildAndCommit());

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<QuickLogResult>().Logger);

var app = builder.Build();

// Dispose on shutdown — flushes the async queue.
app.Lifetime.ApplicationStopping.Register(() =>
    app.Services.GetRequiredService<QuickLogResult>().Dispose());

app.Run();
```

Now any consumer takes `StructuredLogger` via constructor injection.

### 8.2 Console / CLI apps

```csharp
using var herald = QuickLogBuilder.Create("my-cli")
    .WithBridge(datadog)
    .WithAsync()
    .BuildAndCommit();

var log = herald.Logger;
log.Info(new LogCategory("App"), "running");
// ... work ...
// using-disposal flushes the async queue and disposes sinks.
```

Avoid `Environment.Exit()` — it skips the dispose. If you must, call
`herald.Dispose()` before exiting.

### 8.3 Multiple sinks

```csharp
var herald = QuickLogBuilder.Create("my-service")
    .WithMinimumLevel("trace")
    .WithAsync()
    .WithFileSink("logs/app.ndjson", minLevel: "info", name: "main")
    .WithBridge(datadog, minLevel: "warn", name: "alerts")
    .WithConsoleSink(minLevel: "debug", name: "console")
    .BuildAndCommit();
```

Per-sink minimum levels are independent. Set the pipeline-wide level
low enough to admit every event your noisiest sink wants; per-sink
filters narrow from there.

The `name:` parameter is the operator handle for runtime mutation —
see [`adding-sinks.md`](adding-sinks.md) section 6 for the CRUD model.

### 8.4 Path-mixing trap

Don't combine fluent and JSON-config registration for the same
destination. The bridge and the JSON-driven registry are independent
paths into the pipeline; they don't dedupe. The result is two sinks
posting the same event.

```csharp
// Wrong — double-posts.
.WithBridge(datadog)
.WithJsonConfig("herald.json")   // herald.json also lists kind: datadog
```

Pick one. Either bridge the sink or let JSON config drive it.

---

## Part 9 — Verify

### 9.1 The five-minute smoke test

After wiring is done:

1. Run your app.
2. Emit one event at the level you expect to ship:
   `log.Info(LogCategory.App, "smoke test");`
3. Wait 2-5 seconds (network sinks are batched, can take a moment).
4. Check the destination's search UI for the event.

If you don't see it within 30 seconds, head to Part 10.

### 9.2 Confirm no advisories

Pipeline-construction advisories surface through
`herald.Diagnostic.Advisories`. Inspect once at startup:

```csharp
foreach (var advisory in herald.Diagnostic.Advisories)
{
    System.Console.WriteLine($"[{advisory.Severity}] {advisory.Message}");
}
```

A clean pipeline produces zero advisories. The most common one for
sink consumers is: *"Sink 'X' implements INetworkSink but the pipeline
has no async stage. Wrap with .WithAsync() to keep the producer thread
out of network latency."*

### 9.3 Inspect runtime state

```csharp
foreach (var info in herald.Sinks.Values)
    System.Console.WriteLine(
        $"{info.Name,-12} {info.Kind,-12} {info.MinLevel,-6} {info.RunState}");
```

Every sink should be `Live`. If any are `Disabled` or `Test`, that's
intentional or it isn't — check your wire-up.

---

## Part 10 — Troubleshoot

### 10.1 No logs arriving at the destination

In order of frequency:

1. **Minimum level too high.** Your event is `Debug`; the pipeline's
   minimum is `Info`. The event is filtered before the sink sees it.
   Temporarily lower `WithMinimumLevel("trace")` to confirm.
2. **App exited before flush.** Async sinks buffer. `Environment.Exit`
   skips dispose. Use `using var herald = ...` or
   `app.Lifetime.ApplicationStopping.Register(() => herald.Dispose())`.
3. **Endpoint unreachable.** Network sinks fail silently when they
   can't connect. Check the destination's own UI for "no incoming
   events"; check your firewall / VPC routing.
4. **Forgot `BuildAndCommit()`.** Constructing the builder doesn't
   activate the pipeline. `BuildAndCommit()` is what makes it live.
5. **Path-mixing.** See 8.4 — not the cause of *missing* events, but
   of *duplicate* events. Both worth ruling out.

### 10.2 "Sink kind 'X' not found"

Thrown by `BuildAndCommit()` when JSON config references a kind that
isn't in `LogSinkProviderRegistry.Default`.

Cause: the sink assembly hasn't been loaded yet. The
`[ModuleInitializer]` fires only after the CLR has loaded the
assembly. If your `Program.cs` references no type from
`Herald.Sinks.<Name>`, the assembly isn't loaded.

Fix: touch a type from the sink package somewhere. A `using` at the
top of any file works:

```csharp
using Herald.Sinks.Datadog;   // forces assembly load
```

Or call the explicit registration once at startup:

```csharp
Herald.Sinks.Datadog.DatadogSinkRegistration.RegisterAll(
    LogSinkProviderRegistry.Default);
```

### 10.3 NU1008 — "PackageReference items cannot define a value for Version"

Cause: your project uses Central Package Management (a
`Directory.Packages.props` somewhere above the csproj) and the
PackageReference for the Herald sink carries an inline `Version=`
attribute.

Fix: drop the inline version. Either rely on CPM to resolve it
(version listed in your `Directory.Packages.props`) or remove the CPM
config if you didn't intend to use it.

### 10.4 NotSupportedException at pipeline build

Some sinks are code-first only — `XUnitLogSink` (needs an
`ITestOutputHelper` at construction), `TextWriterLogSink` (needs a
`TextWriter` instance), `CoralogixLogSink`, `CouchbaseLogSink`,
`EmailLogSink`, `InfluxDBLogSink`, `TwilioLogSink` (need credentials
that don't fit the declarative shape).

The provider's `CreateSink` throws `NotSupportedException` with a
message pointing to the code-first constructor.

Fix: construct the sink directly and use Part 5 (fluent path) instead
of Part 6 (JSON config).

### 10.5 AOT trim warnings

See Part 11.

### 10.6 The `Disabled` sink puzzle

A sink that suddenly stops accepting events may be in `Disabled`
state. `Disabled` is the operator panic button — flipping it back is
one call:

```csharp
herald.Sinks.SetLive("alerts");
```

See [`adding-sinks.md`](adding-sinks.md) section 6 for the full Live /
Test / Disabled flow.

---

## Part 11 — AOT publishing

### 11.1 Read the sink's `aot_compatible` field

Every sink's `CAPABILITY.yaml` declares whether the sink survives
`PublishAot=true`. The Datadog example:

```yaml
aot_compatible: true
```

Honest values:

- `true` — survives AOT publish. Console, File, Datadog, Splunk, OTLP,
  most HTTP sinks.
- `false` — pulls a reflection-heavy SDK that doesn't AOT-publish
  cleanly. AWS / Azure / GCP cloud-SDK sinks, MongoDB / Cassandra /
  RavenDB drivers, Confluent.Kafka (native dependency).

### 11.2 Mixing AOT and non-AOT sinks

The pipeline runs whichever sinks you wire. If even one sink is
`aot_compatible: false`, your app can't `PublishAot=true` without
losing that sink's functionality.

Two patterns work:

1. **Pure-AOT app.** Use only `aot_compatible: true` sinks. Many of
   the popular targets (Datadog, Splunk, Loki, OTLP, Sentry) are
   AOT-clean.
2. **JIT app, AOT-aware.** Skip `PublishAot=true`. Use any sink. This
   is the common case.

### 11.3 Trim warnings

Some AOT-compatible sinks produce trim warnings during publish if
your code uses generic logging methods. Most warnings come from
`Microsoft.Extensions.Logging` interop or `System.Text.Json` reflection
paths. The Herald.OSS pipeline itself emits no trim warnings.

---

## Appendix A — Sink catalog by category

### Observability platforms

| Package | Kind | Edition |
|---|---|---|
| `Herald.Sinks.Datadog` | `datadog` | Community |
| `Herald.Sinks.Splunk` | `splunk_hec` | Community |
| `Herald.Sinks.Loki` | `loki` | Community |
| `Herald.Sinks.Honeycomb` | `honeycomb` | Community |
| `Herald.Sinks.NewRelicLogs` | `new_relic_logs` | Community |
| `Herald.Sinks.SumoLogic` | `sumo_logic` | Community |
| `Herald.Sinks.LogzIo` | `logz_io` | Community |
| `Herald.Sinks.Mezmo` | `mezmo` | Community |
| `Herald.Sinks.BetterStack` | `better_stack` | Community |
| `Herald.Sinks.SignalFx` | `signalfx` | Community |
| `Herald.Sinks.Dynatrace` | `dynatrace` | Community |
| `Herald.Sinks.Stackify` | `stackify` | Community |
| `Herald.Sinks.Loggly` | `loggly` | Community |
| `Herald.Sinks.Coralogix` | `coralogix` | Community |
| `Herald.Sinks.Axiom` | `axiom` | Community |
| `Herald.Sinks.Lightstep` | `lightstep` | Community |
| `Herald.Sinks.Elasticsearch` | `elasticsearch` | Community |
| `Herald.Sinks.OpenSearch` | `opensearch` | Community |
| `Herald.Sinks.Graylog` | `graylog` | Community |
| `Herald.Sinks.Seq` | `seq` | Community |

### OpenTelemetry

| Package | Kind | Edition |
|---|---|---|
| `Herald.Sinks.Otlp` | `otlp` | Community |
| `Herald.Sinks.OtlpGrpc` | `otlp_grpc` | Community |

### Cloud archive / storage

| Package | Kind | Edition |
|---|---|---|
| `Herald.Sinks.AmazonS3` | `amazon_s3` | Community |
| `Herald.Sinks.AzureBlobStorage` | `azure_blob_storage` | Community |
| `Herald.Sinks.GoogleCloudLogging` | `google_cloud_logging` | Community |
| `Herald.Sinks.AzureAnalytics` | `azure_analytics` | Community |
| `Herald.Sinks.AzureLogAnalyticsDcr` | `azure_log_analytics_dcr` | Community |
| `Herald.Sinks.AzureTableStorage` | `azure_table_storage` | Community |
| `Herald.Sinks.AzureCosmosDB` | `azure_cosmos_db` | Community |
| `Herald.Sinks.ApplicationInsightsSdk` | `application_insights_sdk` | Community |
| `Herald.Sinks.ApplicationInsightsHttp` | `application_insights_http` | Community |
| `Herald.Sinks.BigQuery` | `bigquery` | Community |
| `Herald.Sinks.Parquet` | `parquet` | Community |

### Message bus / streaming

| Package | Kind | Edition |
|---|---|---|
| `Herald.Sinks.Kafka` | `kafka` | Community |
| `Herald.Sinks.Kinesis` | `kinesis` | Community |
| `Herald.Sinks.Pulsar` | `pulsar` | Community |
| `Herald.Sinks.RabbitMQ` | `rabbitmq` | Community |
| `Herald.Sinks.Nats` | `nats` | Community |
| `Herald.Sinks.Mqtt` | `mqtt` | Community |
| `Herald.Sinks.AzureEventHub` | `azure_event_hub` | Community |
| `Herald.Sinks.AzureServiceBus` | `azure_service_bus` | Community |
| `Herald.Sinks.GoogleCloudPubSub` | `google_cloud_pubsub` | Community |
| `Herald.Sinks.Sqs` | `sqs` | Community |
| `Herald.Sinks.ZeroMQ` | `zeromq` | Community |
| `Herald.Sinks.Fluentd` | (migration guide) | — |
| `Herald.Sinks.Logstash` | (migration guide) | — |
| `Herald.Sinks.Vector` | (migration guide) | — |

### Databases

| Package | Kind | Edition |
|---|---|---|
| `Herald.Sinks.PostgreSQL` | `postgresql` | Community |
| `Herald.Sinks.MySQL` | `mysql` | Community |
| `Herald.Sinks.MSSqlServer` | `mssql_server` | Community |
| `Herald.Sinks.SQLite` | `sqlite` | Community |
| `Herald.Sinks.MongoDB` | `mongodb` | Community |
| `Herald.Sinks.ClickHouse` | `clickhouse` | Community |
| `Herald.Sinks.CouchDB` | (migration guide) | — |
| `Herald.Sinks.Couchbase` | `couchbase` | Community |
| `Herald.Sinks.DynamoDB` | `dynamodb` | Community |
| `Herald.Sinks.RavenDB` | `ravendb` | Community |
| `Herald.Sinks.Cassandra` | `cassandra` | Community |
| `Herald.Sinks.InfluxDB` | `influxdb` | Community |
| `Herald.Sinks.TimescaleDB` | (migration guide) | — |

### Alerting / chat

| Package | Kind | Edition |
|---|---|---|
| `Herald.Sinks.PagerDuty` | `pagerduty` | Community |
| `Herald.Sinks.Slack` | `slack` | Community |
| `Herald.Sinks.Discord` | `discord` | Community |
| `Herald.Sinks.MicrosoftTeams` | `microsoft_teams` | Community |
| `Herald.Sinks.Mattermost` | (migration guide) | — |
| `Herald.Sinks.Telegram` | `telegram` | Community |
| `Herald.Sinks.Twilio` | `twilio` | Community |
| `Herald.Sinks.Email` | `email` | Community |

### Error tracking

| Package | Kind | Edition |
|---|---|---|
| `Herald.Sinks.Sentry` | `sentry` | Community |
| `Herald.Sinks.Rollbar` | `rollbar` | Community |
| `Herald.Sinks.Bugsnag` | `bugsnag` | Community |
| `Herald.Sinks.Raygun` | `raygun` | Community |
| `Herald.Sinks.ElmahIo` | `elmah_io` | Community |
| `Herald.Sinks.Exceptionless` | `exceptionless` | Community |
| `Herald.Sinks.Airbrake` | (migration guide) | — |

### Generic transport

| Package | Kind | Edition |
|---|---|---|
| `Herald.Sinks.HttpJson` | `http_json` | Community |
| `Herald.Sinks.TcpJsonLine` | `tcp_json_line` | Community |
| `Herald.Sinks.UdpJsonLine` | `udp_json_line` | Community |
| `Herald.Sinks.Syslog` | `syslog` | Community |
| `Herald.Sinks.GenericWebhook` | `generic_webhook` | Community |
| `Herald.Sinks.Aliyun` | `aliyun_sls` | Community |

### Local / dev

| Package | Kind | Edition |
|---|---|---|
| `Herald.Sinks.File` | `text_file`, `json_file` | Community |
| `Herald.Sinks.Debug` | `debug` | Community |
| `Herald.Sinks.Trace` | `trace` | Community |
| `Herald.Sinks.EventLog` | `event_log` | Community |
| `Herald.Sinks.TextWriter` | `text_writer` (code-first only) | Community |
| `Herald.Sinks.InMemory` | `in_memory` | Community |
| `Herald.Sinks.XUnit` | `xunit` (code-first only) | Community |
| `Herald.Sinks.GodotConsole` | `godot_console` | Community |
| `Herald.Sinks.UnityConsole` | `unity_console` | Community |
| `Herald.Sinks.HelloWorld` | `hello_world` (test-only) | Community |

---

## Appendix B — Cheat sheet

```csharp
// 1. Install
//    dotnet add package Herald.OSS
//    dotnet add package Herald.Sinks.<Name>

// 2. Fluent path
using Herald.Sinks.<Name>;
using MMP.Herald.Events;
using MMP.Herald.Quick;

var sink = new <Name>LogSink(/* connection details */);

using var herald = QuickLogBuilder.Create("my-service")
    .WithMinimumLevel("info")
    .WithAsync()                    // for network/disk sinks
    .WithBridge(sink)
    .BuildAndCommit();

var log = herald.Logger;
log.Info(new LogCategory("App"), "first message");

// 3. JSON-config path
using MMP.Herald.Quick;

using var herald = QuickLogBuilder.Create()
    .WithJsonConfig("herald.json")
    .WithHotReload()
    .WithAsync()
    .BuildAndCommit();

var log = herald.Logger;

// herald.json:
// {
//   "pipelineName": "my-service",
//   "minimumLevel": "info",
//   "sinks": [
//     { "kind": "<sink_kind>", "uri": "...", "host": "...", "alias": "..." }
//   ]
// }

// 4. Verify
foreach (var advisory in herald.Diagnostic.Advisories)
    System.Console.WriteLine($"[{advisory.Severity}] {advisory.Message}");

foreach (var info in herald.Sinks.Values)
    System.Console.WriteLine($"{info.Name} {info.Kind} {info.RunState}");
```

---

## Cross-references

- **Narrative version of this manual** — [`adding-sinks.md`](adding-sinks.md)
- **Writing a new sink** — [`../programming-guide.md`](../programming-guide.md)
- **Testing a sink** — [`../testing-guide.md`](../testing-guide.md)
- **CAPABILITY.yaml schema** — [`../CAPABILITY-SCHEMA.md`](../CAPABILITY-SCHEMA.md)
- **Contributing to Herald.Sinks** — [`../CONTRIBUTING.md`](../CONTRIBUTING.md)
- **Herald.OSS quickstart** — [`Herald.OSS/docs/howtos/HOWTO-QUICKSTART.md`](https://github.com/mmpworks/Herald.OSS/blob/main/docs/howtos/HOWTO-QUICKSTART.md)
- **Herald.OSS sinks reference** — [`Herald.OSS/docs/howtos/HOWTO-SINKS.md`](https://github.com/mmpworks/Herald.OSS/blob/main/docs/howtos/HOWTO-SINKS.md)
- **Herald.OSS operations** — [`Herald.OSS/docs/howtos/HOWTO-OPERATIONS.md`](https://github.com/mmpworks/Herald.OSS/blob/main/docs/howtos/HOWTO-OPERATIONS.md)
