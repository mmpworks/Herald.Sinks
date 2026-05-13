# MMP.Herald.Sinks.Email

> Sends Herald log events as SMTP email via MailKit. Drop-in for Serilog.Sinks.Email. Sized for high-severity alerts — pair with a warn+ or error+ level filter, otherwise mailbox flooding becomes the bug you're trying to solve.

Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package MMP.Herald.Sinks.Email
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `email` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- LogBatch → one digest email containing every event in the batch
- Subject template with {level} placeholder resolves to highest severity
- MailKit STARTTLS / OAuth2 / implicit TLS supported via SecureSocketOptions
- Highest-severity level surfaces in the subject line
- Plain-text body with timestamp / level / category / message / properties

## Limitations

- Synchronous Send (one connect per send)
- No HTML body in 1.0
- Pair with a level filter — never use against info-level chatter

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — each Send call opens its own SmtpClient.

## Vendor

MailKit / RFC 5321 — https://github.com/jstedfast/MailKit

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
