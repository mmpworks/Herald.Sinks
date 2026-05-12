<!--
  Copyright (c) 2026 MMPWorks LLC
  Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
-->

# Herald.Sinks.TimescaleDB

**This is not a NuGet package.** It is a migration guide.

If you came looking for a TimescaleDB sink, the answer is
`Herald.Sinks.PostgreSQL` plus a one-line piece of DDL on the
TimescaleDB side. TimescaleDB is a PostgreSQL extension; the
Herald sink talks plain Postgres wire protocol, and the
hypertable conversion is an operator concern that lives in the
database, not in the sink.

---

## What TimescaleDB adds to PostgreSQL

TimescaleDB takes a regular table and turns it into a
**hypertable**: physically partitioned by time, queried as if it
were one table. You get:

- Automatic chunk-by-time partitioning (default: 1 day per chunk).
- Per-chunk indexes that the planner uses for range scans.
- Continuous aggregates, retention policies, and compression
  policies — all configured in the database.

For a logs workload that means cheap range queries
(`time_utc BETWEEN x AND y`), automatic drop of chunks older than
N days, and ~10× compression on cold chunks. None of that needs
a different sink.

---

## What Herald.Sinks.PostgreSQL does

It opens an Npgsql connection, INSERTs each event with parameterised
SQL, and runs through the same code path on a hypertable as it does
on a regular table. The sink does not care that the table is a
hypertable.

---

## Wiring it up

Two pieces of one-time DDL on the TimescaleDB side:

```sql
CREATE EXTENSION IF NOT EXISTS timescaledb;

CREATE TABLE IF NOT EXISTS herald_logs (
    time_utc        TIMESTAMPTZ NOT NULL,
    level           TEXT NOT NULL,
    category        TEXT,
    message         TEXT,
    template        TEXT,
    properties      JSONB
);

SELECT create_hypertable('herald_logs', 'time_utc',
    chunk_time_interval => INTERVAL '1 day',
    if_not_exists => TRUE);

CREATE INDEX IF NOT EXISTS idx_herald_logs_level
    ON herald_logs (level, time_utc DESC);
```

Then point the Herald sink at it the way you would any Postgres
target:

```csharp
using Herald.Sinks.PostgreSQL;
using MMP.Herald;
using MMP.Herald.Quick;

var sink = new PostgreSQLLogSink(
    connectionString: "Host=tsdb.internal;Database=logs;Username=herald;Password=...",
    tableName: "herald_logs");

var pipeline = QuickLogBuilder.Create("timescale-pipeline")
    .WithMinimumLevel("info")
    .WithBridge(sink)
    .WithAsyncLogging(capacity: 10_000)
    .BuildAndCommit();

pipeline.Logger.Info("Hello from Herald to TimescaleDB.");
```

The Postgres sink batches inserts via `IBatchedLogSink` so a 100-event
batch becomes one round trip. TimescaleDB handles batched inserts
into a hypertable as efficiently as a regular table.

---

## Operator concerns

A few things worth knowing.

**Chunk size.** The default 1-day chunk works well for most logs
workloads. If you write more than ~1 GB per day per source, drop
to 6 hours; for low-volume systems 7 days saves chunk overhead.
Tune via `set_chunk_time_interval`.

**Retention.** Add a retention policy and the database manages
deletion for you:

```sql
SELECT add_retention_policy('herald_logs', INTERVAL '30 days');
```

The sink keeps writing; old chunks evaporate on the schedule. No
application code touches retention.

**Compression.** Enable compression on chunks older than N days
to cut storage 10× without query changes:

```sql
ALTER TABLE herald_logs SET (timescaledb.compress, timescaledb.compress_segmentby = 'level');
SELECT add_compression_policy('herald_logs', INTERVAL '7 days');
```

Compressed chunks stay queryable; the planner decompresses
transparently.

**Continuous aggregates.** For dashboards that count error rates
per minute, build a continuous aggregate so the dashboard query
hits a small materialised rollup instead of the full hypertable:

```sql
CREATE MATERIALIZED VIEW herald_logs_hourly
WITH (timescaledb.continuous) AS
SELECT
    time_bucket(INTERVAL '1 hour', time_utc) AS hour,
    level,
    count(*)
FROM herald_logs
GROUP BY hour, level;
```

---

## Why no dedicated package?

The sink does not need to know anything different. TimescaleDB
exposes a Postgres-compatible wire protocol; the Herald sink
already speaks it. A dedicated package would be a copy of
`Herald.Sinks.PostgreSQL` with a different `category` field in the
manifest — extra surface, no behaviour change.

If you find yourself needing TimescaleDB-specific behaviour
(creating hypertables on first run, registering continuous
aggregates from code) those belong in your operator tooling, not
in the sink. Keep the sink simple. Let the database do the
database thing.
