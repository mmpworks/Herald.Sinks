<!--
  Copyright (c) 2026 MMPWorks LLC
  Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
-->

# Herald.Sinks.Airbrake

**This is not a NuGet package.** It is a migration guide.

If you came looking for `Herald.Sinks.Airbrake`, the answer is
`Herald.Sinks.HttpJson` pointed at Airbrake's notice API. The
official Sharpbrake .NET client has no recent activity, and the
notice API is plain HTTP+JSON, so the cost-of-dependency calculus
favours wiring the HTTP path directly.

If your team already runs Bugsnag or Rollbar, prefer those — Herald
ships first-party sinks for both (`Herald.Sinks.Bugsnag`,
`Herald.Sinks.Rollbar`) and they're actively maintained.

---

## What Airbrake exposes

The notice API takes a JSON body at:

```text
POST https://api.airbrake.io/api/v3/projects/{projectId}/notices
```

Authentication is a project key passed via `?key={projectKey}` or the
`Bearer` header. The body shape is documented at
<https://airbrake.io/docs/api/>; for log events the minimum is
`errors`, `context`, and `params`.

---

## The custom sink

Drop this class into your project. It implements `ILogger`, posts
each event as one notice, and avoids the Sharpbrake dependency.

```csharp
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using MMP.Herald;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;

public sealed class AirbrakeLogSink : ILogger, IDisposable
{
    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly bool _ownsHttp;

    public AirbrakeLogSink(string projectId, string projectKey, string environment = "production", HttpClient? http = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectKey);

        _ownsHttp = http is null;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _endpoint = new Uri($"https://api.airbrake.io/api/v3/projects/{projectId}/notices?key={projectKey}");
        _environment = environment;
    }

    private readonly string _environment;

    public void Log(LogEvent logEvent)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("errors");
            writer.WriteStartObject();
            writer.WriteString("type", logEvent.Category.Value);
            writer.WriteString("message", logEvent.Message ?? string.Empty);
            writer.WriteEndObject();
            writer.WriteEndArray();

            writer.WriteStartObject("context");
            writer.WriteString("environment", _environment);
            writer.WriteString("severity", logEvent.Level.Key);
            writer.WriteEndObject();

            writer.WriteEndObject();
        }
        var body = Encoding.UTF8.GetString(stream.ToArray());
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = _http.PostAsync(_endpoint, content).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() { if (_ownsHttp) _http.Dispose(); }
}
```

Wire it the same way as any other Herald sink:

```csharp
var sink = new AirbrakeLogSink(
    projectId: "12345",
    projectKey: "your-project-key",
    environment: "production");

var pipeline = QuickLogBuilder.Create("airbrake-pipeline")
    .WithMinimumLevel("warn")  // typically you only ship warn+ to error trackers
    .WithBridge(sink)
    .WithAsyncLogging(capacity: 1_000)
    .BuildAndCommit();
```

---

## When to choose something different

- **Already on Sentry?** Use `Herald.Sinks.Sentry`. Better.
- **Want maintained .NET tooling?** Use `Herald.Sinks.Bugsnag` or
  `Herald.Sinks.Rollbar`.
- **OSS budget?** `Herald.Sinks.ElmahIo` and `Herald.Sinks.Exceptionless`
  cover the same job with active SDKs.

Airbrake is on the catalog for completeness; it isn't where new
deployments end up in 2026.
