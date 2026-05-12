<!--
  Copyright (c) 2026 MMPWorks LLC
  Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
-->

# Herald.Sinks.CouchDB

**This is not a NuGet package.** It is a migration guide.

If you searched for `Herald.Sinks.CouchDB` looking for Herald's answer
to `Serilog.Sinks.CouchDB`, you are in the right place. Herald does
not ship a dedicated CouchDB sink because the protocol is plain JSON
over HTTP and a working sink is short enough to copy into your own
project. This guide hands you the snippet and the operator notes that
go with it.

---

## What CouchDB exposes

CouchDB databases accept documents through two paths:

- **Single-doc:** `POST /{db}` with a JSON body inserts one document.
  CouchDB assigns the `_id` if you don't supply one.
- **Bulk:** `POST /{db}/_bulk_docs` with a body shaped as
  `{ "docs": [ … ] }` inserts many documents in one round trip. This
  is the one you want for logs.

Both paths support Basic auth via the `Authorization` header. Cluster
deployments (Cloudant, self-hosted with `[admins]` set) require it.

---

## Why no dedicated package?

`Herald.Sinks.HttpJson` already speaks JSON over HTTP, but its body
shape is **NDJSON** — one event per line, no wrapper. CouchDB's
`_bulk_docs` endpoint expects a single object: `{ "docs": [ … ] }`.
That tiny shape mismatch is the only thing standing between HttpJson
and a CouchDB sink.

Rather than ship a dedicated package for that one wrapper, we hand
you a 30-line custom sink you can drop straight into your project.
You own the credentials, the database name, and the batch shape, and
you avoid taking on Herald.Sinks.CouchDB as a transitive dependency
forever.

---

## The custom sink

Drop this class into your project. It implements `ILogger` and
`IBatchedLogSink` so Herald's pipeline can dispatch single events or
batches without you doing anything special on the call site.

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using MMP.Herald;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;

public sealed class CouchDbLogSink : ILogger, IBatchedLogSink, IDisposable
{
    private readonly HttpClient _http;
    private readonly Uri _bulkEndpoint;
    private readonly bool _ownsHttp;

    // databaseName matches a CouchDB database you've already created.
    // The HttpClient is yours to share — pass null to let the sink build
    // its own (in which case the sink disposes it).
    public CouchDbLogSink(string baseUrl, string databaseName, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        _ownsHttp = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _bulkEndpoint = new Uri(new Uri(baseUrl), $"{databaseName}/_bulk_docs");
    }

    public void Log(LogEvent logEvent) =>
        LogBatch(new[] { logEvent });

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        if (events.Count == 0) return;

        var body = BuildBulkBody(events);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = _http.PostAsync(_bulkEndpoint, content).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }

    // Body shape: { "docs": [ {event}, {event}, ... ] }.
    // Utf8JsonWriter keeps the path AOT-clean.
    private static string BuildBulkBody(IReadOnlyList<LogEvent> events)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("docs");
            foreach (var evt in events)
            {
                writer.WriteStartObject();
                writer.WriteString("time_utc", evt.TimeUtc);
                writer.WriteString("level", evt.Level.Key);
                writer.WriteString("category", evt.Category.Value);
                writer.WriteString("message", evt.Message ?? string.Empty);
                writer.WriteString("template", evt.MessageTemplate ?? string.Empty);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
```

---

## Wiring it into a pipeline

```csharp
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using MMP.Herald;
using MMP.Herald.Quick;

// Share one HttpClient across your process; HttpClient is thread-safe
// and pools connections internally.
var http = new HttpClient { BaseAddress = null };
var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes("herald:supersecret"));
http.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Basic", basic);

var sink = new CouchDbLogSink(
    baseUrl: "https://couchdb.internal:6984/",
    databaseName: "herald-logs",
    httpClient: http);

var pipeline = QuickLogBuilder.Create("couchdb-pipeline")
    .WithMinimumLevel("info")
    .WithBridge(sink)
    .WithAsyncLogging(capacity: 10_000)
    .BuildAndCommit();

pipeline.Logger.Info("Hello from Herald to CouchDB.");
```

`WithAsyncLogging` keeps HTTP latency off the calling thread; the
async worker drains batches into the sink on its own.

---

## What ends up in CouchDB

Each Herald event becomes one CouchDB document. A typical document
looks like this:

```json
{
    "_id": "9f4c4e8e-...",
    "_rev": "1-abc...",
    "time_utc": "2026-04-25T16:42:11.341Z",
    "level": "info",
    "category": "Auth",
    "message": "User signed in",
    "template": "User signed in"
}
```

`_id` and `_rev` come from CouchDB. The rest is the event shape from
the snippet above. Add fields to `BuildBulkBody` if you want
properties or context to ride along.

---

## Querying the logs

CouchDB has Mango (a JSON query language) and views (JavaScript
map/reduce). Two queries you'll write often:

**By level (Mango):**

```bash
curl -X POST https://couchdb.internal:6984/herald-logs/_find \
     -u herald:supersecret \
     -H 'Content-Type: application/json' \
     -d '{
       "selector": { "level": "error" },
       "sort": [{ "time_utc": "desc" }],
       "limit": 100
     }'
```

**Time range (view):** create a view that emits `time_utc` as the key,
then query with `?startkey=...&endkey=...`. The view is incrementally
maintained, so range scans stay cheap.

---

## Operator concerns

A few things worth knowing before you run this in production.

**Compaction.** CouchDB keeps every revision until you compact. A
high-volume log database grows fast. Schedule periodic compaction
(`POST /{db}/_compact`) or move old days to a cold-storage database
and drop them.

**Database per day.** A common pattern is `herald-logs-2026-04-25` —
one database per day. Drop the old database when you're done with it;
the data goes with it. Cheaper than compaction. The trade-off is that
queries spanning days have to hit multiple databases.

**Batch size.** CouchDB happily accepts thousands of docs per
`_bulk_docs` call, but latency grows. 200–500 events per batch is a
reasonable sweet spot. Tune the Herald pipeline's batch size to
match.

**TLS.** CouchDB's default config is HTTP. Put TLS in front via a
reverse proxy (nginx, Caddy) for any non-loopback deployment. Herald
sends whatever URL you point it at; HTTPS just works.

**Auth rotation.** Basic auth is fine for a single shared credential.
For per-service credentials and rotation, run CouchDB with `_session`
cookie auth or front it with an OAuth proxy. Either path swaps
headers on the `HttpClient` at runtime.

---

## When to do something different

A dedicated package would buy you these things:

- Auto-create the database on first write.
- Build CouchDB design documents (views, Mango indexes) at startup.
- Replication trigger management.

If you find yourself writing those, you have a CouchDB-specific
operations layer, not a logging sink. Keep the sink simple. Put the
operations work next to the rest of your CouchDB tooling, where it
belongs.
