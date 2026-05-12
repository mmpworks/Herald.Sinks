<!--
  Copyright (c) 2026 MMPWorks LLC
  Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
-->

# Herald.Sinks.Fluentd

**This is not a NuGet package.** It is a migration guide.

If you came looking for `Herald.Sinks.Fluentd` (or
`Herald.Sinks.FluentBit`), the answer is `Herald.Sinks.HttpJson`
pointed at the Fluentd `in_http` source. Herald already speaks the
format Fluentd accepts.

---

## What Fluentd exposes

Fluentd's `in_http` plugin (and Fluent Bit's matching `http`
input) opens a port and accepts JSON over HTTP. The URL path is
the **tag** — Fluentd's routing key — so requests to
`/herald.app.access` arrive in the pipeline tagged
`herald.app.access`. The body is either a single JSON object, an
array of JSON objects, or newline-delimited JSON depending on the
configured format.

---

## Fluentd side

`fluent.conf`:

```text
<source>
  @type http
  port 9880
  bind 0.0.0.0
  body_size_limit 32m
  keepalive_timeout 10s
</source>

<match herald.**>
  @type elasticsearch
  host es.internal
  port 9200
  index_name herald-logs
  type_name _doc
  flush_interval 5s
</match>
```

Fluent Bit (`fluent-bit.conf`) is similar:

```text
[INPUT]
    Name          http
    Listen        0.0.0.0
    Port          9880

[OUTPUT]
    Name          es
    Match         herald.*
    Host          es.internal
    Port          9200
    Index         herald-logs
```

Restart, confirm it's listening, and you're done.

---

## Herald side

```csharp
using Herald.Sinks.HttpJson;
using MMP.Herald;
using MMP.Herald.Levels;
using MMP.Herald.Quick;

var levels = new DefaultLogLevelRegistryFactory().Create();

// The path is the Fluentd tag. /herald.app.access arrives in
// the pipeline tagged 'herald.app.access' for downstream routing.
var sink = new HttpJsonLogSink(
    uri: "http://fluentd.internal:9880/herald.app.events",
    levelRegistry: levels);

var pipeline = QuickLogBuilder.Create("fluentd-pipeline")
    .WithMinimumLevel("info")
    .WithBridge(sink)
    .WithAsyncLogging(capacity: 10_000)
    .BuildAndCommit();

pipeline.Logger.Info("Hello from Herald to Fluentd.");
```

`HttpJsonLogSink` ships NDJSON with `Content-Type:
application/x-ndjson`. Fluentd's `in_http` plugin auto-detects the
`Content-Type` and treats each line as an event.

---

## Operator concerns

**Tags = routes.** Pick the URL path carefully. Fluentd's `<match>`
selectors run on the tag, so `herald.app.errors` lets you route a
specific level upstream:

```csharp
var infoSink  = new HttpJsonLogSink("http://fluentd:9880/herald.app.info",  levels);
var errorSink = new HttpJsonLogSink("http://fluentd:9880/herald.app.error", levels);
```

Or send everything to one tag and filter inside Fluentd. Either
pattern works.

**Backpressure.** Fluentd's `in_http` plugin returns HTTP 503 when
its memory buffer is full. `HttpJsonLogSink.LogBatch` calls
`EnsureSuccessStatusCode` and throws on non-2xx, which surfaces as
a sink failure. Pair with `WithAsyncLogging` so the failure stays
on the worker thread and doesn't propagate back to the call site.

**Auth.** Fluentd's `in_http` plugin supports basic auth via
`<auth>` blocks. Wire matching credentials onto the `HttpClient`
you pass to `HttpJsonLogSink`.

**Fluent Bit memory.** Fluent Bit is significantly lighter than
Fluentd (Go vs Ruby). For sidecar / DaemonSet patterns Fluent Bit
is usually the right choice. The Herald sink doesn't change.

---

## Why no dedicated package?

Fluentd is a transport. The destination is whatever the
`<match>` blocks point at — Elasticsearch, S3, Kafka, BigQuery.
A dedicated package would be `HttpJson` with a different URL
convention.

If you specifically need Fluentd's **forward protocol** (the
binary MessagePack format used by `out_forward`) instead of HTTP,
that's a different story — open an issue and we can discuss.
HTTP is what 95% of teams actually use to feed events in.
