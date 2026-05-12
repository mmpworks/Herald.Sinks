<!--
  Copyright (c) 2026 MMPWorks LLC
  Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
-->

# Herald.Sinks.Lightstep

**This is not a NuGet package.** It is a migration guide.

If you came looking for `Herald.Sinks.Lightstep`, the answer is
`Herald.Sinks.Otlp` pointed at the Lightstep ingest URL with the
access-token header. Lightstep (now Splunk Observability Cloud)
accepts OTLP natively, and Herald already ships an OTLP sink.

---

## Lightstep side

Lightstep's OTLP ingest endpoints:

- **Logs (HTTP/JSON):** `https://ingest.lightstep.com/v1/logs`
- **Logs (HTTP/protobuf):** `https://ingest.lightstep.com/v1/logs`
  with `Content-Type: application/x-protobuf`

Authentication is the `lightstep-access-token` header. You get this
from your Lightstep project's **Project Settings → Access Tokens**.

---

## Herald side

```csharp
using System;
using System.Net.Http;
using Herald.Sinks.Otlp;
using MMP.Herald;
using MMP.Herald.Levels;
using MMP.Herald.Quick;

var http = new HttpClient();
http.DefaultRequestHeaders.Add("lightstep-access-token", "your-token-here");

var levels = new DefaultLogLevelRegistryFactory().Create();

// JSON variant
var sink = new OtlpJsonLogSink(
    endpoint: "https://ingest.lightstep.com/v1/logs",
    levelRegistry: levels,
    httpClient: http);

// or Protobuf variant for slightly tighter wire size
// var sink = new OtlpProtobufLogSink(
//     endpoint: "https://ingest.lightstep.com/v1/logs",
//     levelRegistry: levels,
//     httpClient: http);

var pipeline = QuickLogBuilder.Create("lightstep-pipeline")
    .WithMinimumLevel("info")
    .WithBridge(sink)
    .WithAsyncLogging(capacity: 10_000)
    .BuildAndCommit();

pipeline.Logger.Info("Hello from Herald to Lightstep.");
```

---

## Why no dedicated package?

The protocol is the contract. Lightstep accepts OTLP, Herald speaks
OTLP. A dedicated package would be a copy of `Herald.Sinks.Otlp`
with a different default URL and one extra header — not enough
behaviour to justify the dependency.

If you're standing up a fresh observability stack: also consider
sending the same OTLP stream to Honeycomb, Grafana Tempo, or
Coralogix. They all accept OTLP. You can wire all of them as
parallel sinks in the same Herald pipeline if you want a
multi-vendor strategy.
