<!--
  Copyright (c) 2026 MMP LLC
  Licensed under the MIT License. See LICENSE in the project root.
-->

# Herald.Sinks.Snowflake

**This is not a NuGet package.** It is a migration guide.

If you came looking for a Snowflake sink, the answer for production
log volumes is **Snowpipe over staged files**, not a .NET driver.
This is also Snowflake's own recommendation for streaming inserts.

We deliberately do not ship a `Snowflake.Data`-based sink today.
Two reasons:

1. The current `Snowflake.Data` 4.x line carries unpatched advisories
   (GHSA-2mqw-rq5m-8hc8, GHSA-c82r-c9f7-f5mj) and pulls a vulnerable
   `log4net` 2.0.12 transitively. We keep `TreatWarningsAsErrors`
   plus NU1902/NU1901 enabled across the catalog; suppressing them
   for one sink would break the contract.
2. Streaming `INSERT` against a Snowflake warehouse is the most
   expensive way to land data per row. Snowpipe charges per-credit
   for a continuous load process; for log workloads it is
   substantially cheaper than running a warehouse on inserts.

---

## The recommended pattern

```text
Herald → AmazonS3 sink → S3 bucket → Snowpipe → Snowflake table
```

**Step 1 — write logs as JSON files to S3:**

```csharp
using Herald.Sinks.AmazonS3;
using MMP.Herald;
using MMP.Herald.Quick;
using Amazon;

var s3 = new AmazonS3LogSink(
    bucketName: "company-logs",
    region: RegionEndpoint.USEast1,
    keyPrefix: "herald/",
    rollIntervalMinutes: 5);

var pipeline = QuickLogBuilder.Create("snowflake-via-s3")
    .WithMinimumLevel("info")
    .WithBridge(s3)
    .WithAsyncLogging(capacity: 50_000)
    .BuildAndCommit();
```

**Step 2 — point Snowpipe at the S3 prefix:**

```sql
CREATE OR REPLACE STAGE herald_logs_stage
    URL = 's3://company-logs/herald/'
    STORAGE_INTEGRATION = my_s3_integration
    FILE_FORMAT = (TYPE = JSON);

CREATE OR REPLACE PIPE herald_logs_pipe
    AUTO_INGEST = TRUE
    AS
    COPY INTO herald_logs (time_utc, level, category, message, template)
    FROM (
      SELECT
        $1:time_utc::TIMESTAMP_NTZ,
        $1:level::STRING,
        $1:category::STRING,
        $1:message::STRING,
        $1:template::STRING
      FROM @herald_logs_stage
    )
    FILE_FORMAT = (TYPE = JSON);
```

Wire S3 → SQS → Snowpipe per the
[official Snowpipe S3 setup](https://docs.snowflake.com/en/user-guide/data-load-snowpipe-auto-s3)
and you have continuous ingest with no .NET driver in the path.

---

## Azure Blob and GCS variants

The same pattern works on Azure (`Herald.Sinks.AzureBlobStorage` →
Snowpipe with Azure storage integration) and GCP (write to GCS with
a custom bridge or via BigQuery + scheduled Snowflake transfer).

---

## When you really need direct INSERTs

For ad-hoc, low-volume scenarios where you absolutely need to drive
INSERTs from .NET (e.g. a one-shot test harness), use the Snowflake
JDBC driver under JNBridge or a Python service that calls
`snowflake-connector-python`. Both are actively maintained and have
the patched advisory line.

We will revisit a first-party `Herald.Sinks.Snowflake` package once
`Snowflake.Data` ships a clean line. Until then, stage to S3 and
Snowpipe — it's faster, cheaper, and doesn't carry unpatched CVEs.
