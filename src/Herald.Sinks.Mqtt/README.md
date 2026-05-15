# Herald.Sinks.Mqtt

> Publishes Herald log events as JSON messages to an MQTT broker via MQTTnet. IoT-flavoured pub/sub against HiveMQ, Mosquitto, EMQX, and the AWS IoT Core / Azure IoT Hub MQTT endpoints.

Part of [Herald](https://github.com/mmpworks/Herald.OSS) — high-performance structured logging for .NET 8, 9, and 10.

## Install

```bash
dotnet add package Herald.Sinks.Mqtt
```

The sink auto-registers into `LogSinkProviderRegistry.Default` via a `[ModuleInitializer]` on assembly load. No manual `RegisterAll(...)` or `With*SinkProviders()` call is required — `dotnet add package` is the whole workflow.

Sink kind: `mqtt` (the identifier the Dashboard form and JSON config use to reference this sink).

## Capabilities

- PublishAsync per event
- QoS AtMostOnce by default; bump via the code-first overload (AtLeastOnce, ExactlyOnce)
- Topic supports MQTT hierarchy (herald/logs/error, fleet/+/health)
- Code-first overload accepts a pre-built IMqttClient for shared client + custom auth

## Limitations

- Synchronous Log path uses GetAwaiter().GetResult around the async SDK
- No TLS / client-cert helpers — pass a pre-configured IMqttClient via the code-first ctor
- Connection happens in the connection-string ctor; failure throws at construction

## Tier & runtime

- **Edition**: Community — works on the free Apache 2.0 Herald.Core. No license key required.
- **AOT-compatible**: yes
- **Targets**: .NET 8 / 9 / 10
- **Thread safety**: Thread-safe — IMqttClient is thread-safe per MQTTnet contract.

## Vendor

Eclipse / OASIS MQTT — https://mqtt.org

## Configuration

Per-sink config form lives in `configuration*.mmpform` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See `CAPABILITY.yaml` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).

## License

Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.

---

*Generated from `CAPABILITY.yaml`. Re-run `Modules/Herald.Sinks/tools/generate-readmes.cjs` after manifest edits to refresh.*
