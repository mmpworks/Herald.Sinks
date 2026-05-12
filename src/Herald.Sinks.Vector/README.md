<!--
  Copyright (c) 2026 MMPWorks LLC
  Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
-->

# Herald.Sinks.Vector

**This is not a NuGet package.** It is a migration guide.

If you came looking for `Herald.Sinks.Vector`, the answer is
`Herald.Sinks.HttpJson` pointed at Vector's HTTP source. Vector's
configuration language plus Herald's NDJSON output line up
cleanly.

---

## What Vector is

Vector (vector.dev) is a Rust-based observability data pipeline.
It accepts logs, metrics, and traces from many sources, runs them
through a configurable transform graph (VRL — Vector Remap
Language), and ships them to many sinks. It's a strong alternative
to Fluentd / Fluent Bit, and it's where a lot of the modern
"unified observability" agents are converging.

For Herald, Vector is just another HTTP destination — but the
expressiveness of Vector's transforms is what you actually buy
into.

---

## Vector side

`vector.toml`:

```toml
[sources.herald_in]
type    = "http_server"
address = "0.0.0.0:8686"
encoding = "ndjson"

[transforms.parse_level]
type   = "remap"
inputs = ["herald_in"]
source = '''
.severity = .level
del(.level)
'''

[sinks.elastic_out]
type    = "elasticsearch"
inputs  = ["parse_level"]
endpoints = ["http://es.internal:9200"]
mode    = "data_stream"
```

The `http_server` source listens on 8686. The `remap` transform
renames fields with VRL. The `elasticsearch` sink ships the result
to ES. Swap the sink for `console` / `aws_s3` / `kafka` / `loki` /
`datadog_logs` — same source feeds them all.

---

## Herald side

```csharp
using Herald.Sinks.HttpJson;
using MMP.Herald;
using MMP.Herald.Levels;
using MMP.Herald.Quick;

var levels = new DefaultLogLevelRegistryFactory().Create();

var sink = new HttpJsonLogSink(
    uri: "http://vector.internal:8686/",
    levelRegistry: levels);

var pipeline = QuickLogBuilder.Create("vector-pipeline")
    .WithMinimumLevel("info")
    .WithBridge(sink)
    .WithAsyncLogging(capacity: 10_000)
    .BuildAndCommit();

pipeline.Logger.Info("Hello from Herald to Vector.");
```

`HttpJsonLogSink` produces NDJSON. Vector's `http_server` source
with `encoding = "ndjson"` parses one event per line. Match.

---

## Operator concerns

**Why Vector instead of Fluentd or Logstash.** Vector's transform
graph is type-checked at config load. A typo in a field name
fails at boot, not at the first event. The runtime is Rust;
memory and CPU sit well below Fluentd's Ruby footprint. If you're
greenfield, Vector usually wins.

**Backpressure.** Vector's HTTP source applies backpressure
through the response cycle when the downstream is slow. Same
behaviour as Logstash: pair the sink with `WithAsyncLogging` so
the calling thread never blocks.

**TLS.** Set `tls.enabled = true` on the source and Vector
listens on HTTPS. Herald follows the URL scheme — flip
`http://` to `https://`.

**Auth.** The `http_server` source supports `auth.strategy =
"basic"`. Wire matching credentials on the `HttpClient` passed to
`HttpJsonLogSink`.

**Disk buffering.** Vector ships per-sink disk buffers
(`buffer.type = "disk"`). For survive-Vector-restart durability,
turn it on at the sink level — the Herald side does not change.

---

## Why no dedicated package?

Vector accepts NDJSON over HTTP. `Herald.Sinks.HttpJson` produces
NDJSON over HTTP. The protocol is the contract, not the agent
brand. A dedicated package would be a thin alias.

If you need Vector's **native protocol** (the gRPC `vector` source
that connects two Vector instances) that's a different story —
the source is meant for Vector-to-Vector hops, not application
ingest. Use HTTP.
