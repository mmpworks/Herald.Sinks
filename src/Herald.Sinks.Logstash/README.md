<!--
  Copyright (c) 2026 MMPWorks LLC
  Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
-->

# Herald.Sinks.Logstash

**This is not a NuGet package.** It is a migration guide.

If you came looking for `Herald.Sinks.Logstash`, the answer is
`Herald.Sinks.HttpJson` pointed at Logstash's HTTP input plugin.
Herald already speaks the format Logstash expects.

---

## What Logstash exposes

The `logstash-input-http` plugin opens a port (default 8080) and
accepts events over HTTP. You configure the codec — `json` for
single-event bodies, `json_lines` for newline-delimited JSON. The
plugin runs inside the same Logstash pipeline as your file/syslog/
beats inputs, so events from Herald flow through the same filter
and output stages.

---

## What Herald.Sinks.HttpJson does

It POSTs each event as a single JSON line. With `IBatchedLogSink`
batching, multiple events in one HTTP request become a multi-line
NDJSON body — exactly what `json_lines` expects.

---

## Logstash side

Add the input to your Logstash config:

```text
input {
  http {
    port => 8080
    codec => json_lines
    additional_codecs => { "application/x-ndjson" => "json_lines" }
  }
}

filter {
  # Your existing filter pipeline.
}

output {
  elasticsearch {
    hosts => ["http://es.internal:9200"]
    index => "herald-logs-%{+YYYY.MM.dd}"
  }
}
```

Restart Logstash, confirm it's listening on 8080, and you're done
on that side.

---

## Herald side

```csharp
using Herald.Sinks.HttpJson;
using MMP.Herald;
using MMP.Herald.Levels;
using MMP.Herald.Quick;

var levels = new DefaultLogLevelRegistryFactory().Create();

var sink = new HttpJsonLogSink(
    uri: "http://logstash.internal:8080/",
    levelRegistry: levels);

var pipeline = QuickLogBuilder.Create("logstash-pipeline")
    .WithMinimumLevel("info")
    .WithBridge(sink)
    .WithAsyncLogging(capacity: 10_000)
    .BuildAndCommit();

pipeline.Logger.Info("Hello from Herald to Logstash.");
```

`Herald.Sinks.HttpJson` produces NDJSON with `Content-Type:
application/x-ndjson`, so the Logstash input plugin's
`json_lines` codec parses one event per line.

---

## Operator concerns

**Authentication.** The HTTP input plugin supports Basic auth via
`user` / `password`. Wire the matching credentials onto the
`HttpClient` you pass into `HttpJsonLogSink`:

```csharp
var http = new HttpClient();
var basic = Convert.ToBase64String(
    Encoding.UTF8.GetBytes("herald:supersecret"));
http.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Basic", basic);

var sink = new HttpJsonLogSink(
    uri: "https://logstash.internal:8080/",
    levelRegistry: levels,
    httpClient: http);
```

**TLS.** The plugin also supports TLS via `ssl_certificate` /
`ssl_key`. Herald sends whatever scheme you point it at; HTTPS
just works once you set the URL.

**Backpressure.** Logstash applies backpressure by stalling the HTTP
response when the pipeline is full. `Herald.Sinks.HttpJson` waits on
the response, which can stall the calling thread. Pair the sink
with `WithAsyncLogging(capacity: ...)` so the calling thread never
sees the wait.

**Persistent queues.** Enable Logstash's persistent queue
(`queue.type: persisted` in `logstash.yml`) if you can't tolerate
events lost during a Logstash restart. The sink doesn't change.

---

## Why no dedicated package?

Logstash is a transport, not a destination. Once events land at the
HTTP input, the destination is whatever your Logstash output stage
points at — Elasticsearch, S3, Kafka, files. A dedicated
`Herald.Sinks.Logstash` package would be a thin alias of
`HttpJson` with one URL convention baked in, which is not enough
behaviour to justify the dependency.

If your team's pattern is "everything goes through Logstash," the
`HttpJson` sink with the URL pointed at the right input plugin is
exactly that pattern, and the operator team owns the rest of the
pipeline.
