<!--
  Copyright (c) 2026 MMP LLC
  Licensed under the MIT License. See LICENSE in the project root.
-->

# Herald.Sinks.Mattermost

**This is not a NuGet package.** It is a migration guide.

If you came looking for `Herald.Sinks.Mattermost`, the answer is
`Herald.Sinks.Slack` pointed at a Mattermost incoming webhook.
Mattermost's webhook accepts the same `{"text": "..."}` body shape
as Slack — they're protocol-compatible by design.

---

## Mattermost side

In your Mattermost server's **Integrations → Incoming Webhooks**,
create a new webhook for the channel you want logs in. Mattermost
gives you a URL of the form:

```text
https://mattermost.your-company.com/hooks/abc123def456...
```

That URL is your sink target.

---

## Herald side

```csharp
using Herald.Sinks.Slack;
using MMP.Herald;
using MMP.Herald.Quick;

var sink = new SlackLogSink(
    webhookUrl: "https://mattermost.your-company.com/hooks/abc123def456");

var pipeline = QuickLogBuilder.Create("mattermost-pipeline")
    .WithMinimumLevel("warn")  // SaaS chat tools dislike spam
    .WithBridge(sink)
    .WithAsyncLogging(capacity: 1_000)
    .BuildAndCommit();

pipeline.Logger.Warn("Hello from Herald to Mattermost.");
```

Same builder pattern, same level filtering, same async wrapping.
The webhook URL is the only thing that distinguishes Mattermost
from Slack.

---

## Mattermost-only fields

Mattermost's webhook accepts a few fields Slack ignores:

- `username` — override the bot's display name per message
- `icon_url` — override the bot's avatar per message
- `channel` — post to a different channel than the webhook's default
- `attachments` — same shape as Slack's attachments

`Herald.Sinks.Slack` ships the basic `text` field today. If you need
attachments or channel routing, either subclass `SlackLogSink` to
extend the body, or write a small custom sink that includes the
extra fields. Both approaches stay within Herald's `ILogger` contract.

---

## Why no dedicated package?

The protocol contract is identical for the basic case. A separate
`Herald.Sinks.Mattermost` would be a copy of `Herald.Sinks.Slack`
with one comment changed — not enough behaviour to justify the
package. The migration guide makes the relationship explicit and
points users at the right artefact.
