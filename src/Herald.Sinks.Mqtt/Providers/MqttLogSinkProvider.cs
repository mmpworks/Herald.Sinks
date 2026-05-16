// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using MMP.Herald;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Levels;
using MMP.Herald.Output.Rendering;
using MMP.Herald.Pipeline;
using MMP.Herald.Routing;

namespace Herald.Sinks.Mqtt.Providers;

public sealed class MqttLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "mqtt";
    public string SinkKind => KindKey;
    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);
        var topic = string.IsNullOrWhiteSpace(definition.Host) ? "herald/logs" : definition.Host;
        // Uri is "host:port"; default to 1883.
        var (host, port) = ParseHostPort(definition.Uri);
        return new MqttLogSink(host, port, topic);
    }

    private static (string Host, int Port) ParseHostPort(string raw)
    {
        var trimmed = raw.Trim();
        var colon = trimmed.IndexOf(':');
        if (colon < 0) return (trimmed, 1883);
        var host = trimmed[..colon];
        return int.TryParse(trimmed[(colon + 1)..], out var port) ? (host, port) : (host, 1883);
    }
}
