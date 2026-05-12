<!--
  Copyright (c) 2026 MMPWorks LLC
  Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
-->

# Herald.Sinks.Map

**This is not a NuGet package.** It is a migration guide.

If you searched for `Herald.Sinks.Map` looking for Herald's answer to
`Serilog.Sinks.Map`, you are in the right place. Herald does not ship a
`Map` sink because Herald's routing layer covers the same job directly —
without the wrapper. This doc shows you exactly how to set that up.

---

## What Serilog.Sinks.Map does

`Serilog.Sinks.Map` is a **dispatcher**. When a log event comes in,
it looks at one property on the event (say, `TenantId`) and picks which
child sink to send the event to based on that value.

Typical shape:

```csharp
// Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Map("TenantId", (tenantId, wt) =>
        wt.File($"logs/{tenantId}.log"))
    .CreateLogger();
```

Each event gets written to a file named after the tenant. `acme`'s
events land in `logs/acme.log`, `globex`'s in `logs/globex.log`.

---

## What Herald does instead

Herald already has a routing layer between the application and the
sinks. You point each event at a destination by writing a **filter
expression** for the route, instead of wrapping a dispatcher around
one property lookup.

Two common shapes:

1. **One pipeline, multiple sinks, each with its own filter.** The
   simplest and most common case. Every sink declares which events it
   wants using a small query string.

2. **Bridges between pipelines.** If you want a whole pipeline per
   tenant — with different enrichers, processors, or retention — build
   a second pipeline and bridge events across with a predicate.

Pick the shape that matches the level of separation you want.

---

## Migration, step by step

### Goal: one log file per tenant

You have a handful of tenants. Each tenant gets its own log file. The
same application code emits a `TenantId` property on every event.

#### Step 1. Make sure the property is set on every event

Before any routing works, Herald needs to see the property on the log
event. The easiest way is a scope:

```csharp
using (logger.PushProperty("TenantId", currentTenantId))
{
    logger.Info(category, "processing request");
}
```

Every event inside the `using` block carries `TenantId` automatically.

#### Step 2. Register one file sink per tenant with a filter

```csharp
using MMP.Herald.Quick;

var logger = QuickLogBuilder
    .Create("app")
    .WithMinimumLevel("info")
    // Sink A: only acme's events
    .WithFileSink("logs/acme.log",
        name: "acme",
        filterExpression: "TenantId = 'acme'")
    // Sink B: only globex's events
    .WithFileSink("logs/globex.log",
        name: "globex",
        filterExpression: "TenantId = 'globex'")
    // Sink C: everything else (default landing spot)
    .WithFileSink("logs/unmapped.log",
        name: "unmapped",
        filterExpression: "TenantId != 'acme' AND TenantId != 'globex'")
    .BuildAndCommit()
    .Logger;
```

That's the whole setup. No wrapper sink, no callback, no child logger
construction per tenant.

#### Step 3. Write logs normally

```csharp
using (logger.PushProperty("TenantId", "acme"))
{
    logger.Info("orders", "order {OrderId} submitted", 42);
}
// ↑ lands in logs/acme.log only
```

The filter expression on each sink decides at runtime which events
pass. Sinks whose filter rejects the event never see it — they don't
pay any cost for it.

### Goal: a whole separate pipeline per tenant

Sometimes tenants need different **behavior**, not just different
files. Maybe `acme` runs with extra enrichers and a Slack alert sink;
`globex` has a compliance audit sink that `acme` does not. For that,
build one pipeline per tenant and bridge events from the main
pipeline.

```csharp
var acmePipeline = QuickLogBuilder
    .Create("acme")
    .WithFileSink("logs/acme.log")
    .WithSlackSink(channelUrl: acmeSlackUrl)
    .BuildAndCommit()
    .Logger;

var mainPipeline = QuickLogBuilder
    .Create("main")
    .WithConsoleSink()
    .WithBridge(acmePipeline,
        predicate: e => e.Properties["TenantId"]?.ToString() == "acme")
    .BuildAndCommit()
    .Logger;
```

The bridge forwards events that match the predicate to the acme
pipeline. Events that don't match only see the main pipeline's sinks.

---

## What's NEW and IMPROVED in the Herald approach

| Serilog.Sinks.Map | Herald routing |
|---|---|
| One property, one dispatcher | Any filter expression — multiple fields, operators, regex |
| Wrapper sink around the real sinks | Flat pipeline — filters live on the sinks themselves |
| Same configuration per dispatched sink | Each destination is configured independently |
| No way to reject without a sink | Events with no matching sink simply drop |
| Silent child-sink errors by default | Errors go to Herald's failure-sink channel with the offending sink named |
| Property values become sink keys (unbounded → unbounded files) | Filters are predicates, not keys — no risk of runaway file creation from hostile input |

**Filter expression syntax** (brief):

| Operator | What it does |
|---|---|
| `=` / `!=` | Equality / inequality |
| `~` | Regex match |
| `<`, `<=`, `>`, `>=` | Numeric / string comparison |
| `AND`, `OR`, `NOT` | Boolean composition |
| `Context.User.Tenant` | Dotted field paths |

Example:
`"Level = 'error' AND (TenantId = 'acme' OR TenantId = 'globex')"`

---

## Full working example

```csharp
using MMP.Herald.Quick;

// Build the pipeline once at startup.
var logger = QuickLogBuilder
    .Create("app")
    .WithMinimumLevel("info")
    .WithFileSink("logs/acme.log",
        name: "acme-file",
        filterExpression: "TenantId = 'acme'")
    .WithFileSink("logs/globex.log",
        name: "globex-file",
        filterExpression: "TenantId = 'globex'")
    .WithConsoleSink(name: "ops-console")  // sees every event
    .BuildAndCommit()
    .Logger;

// Your app code sets the property and logs normally.
void HandleRequest(string tenantId, string userId)
{
    using (logger.PushProperty("TenantId", tenantId))
    using (logger.PushProperty("UserId", userId))
    {
        logger.Info("requests", "request received");
        // ...work...
        logger.Info("requests", "request completed");
    }
}
```

---

## Troubleshooting

**My tenant's events are going to every file.**
Check that the filter expression on each sink is quoted correctly —
`TenantId = 'acme'` (string literal in single quotes), not
`TenantId = acme`.

**My tenant's events are going to no file.**
The `TenantId` property is not making it onto the event. Confirm
`PushProperty` is in scope when the log call fires, or that your
enricher sets the property before routing runs.

**Property names are case-sensitive.**
Filter expressions are case-sensitive on both field names and values.
`tenantId = 'acme'` will not match an event with property `TenantId`.

**I want a catch-all for unrecognized tenants.**
Add a sink with a "not any of" filter, like the `unmapped.log` sink in
the first example above.

---

## See also

- `Modules/Core/docs/under-the-hood.md` Section 3 — the routing layer
- `Modules/Core/src/Predicates/` — the filter-expression compiler
- `QuickLogBuilder.WithFilterExpression()` — pipeline-wide filtering
- `QuickLogBuilder.WithBridge()` — cross-pipeline forwarding
