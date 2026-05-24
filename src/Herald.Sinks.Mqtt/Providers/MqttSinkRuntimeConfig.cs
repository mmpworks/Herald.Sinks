// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using MMP.Herald.Configuration.Runtime;

namespace Herald.Sinks.Mqtt.Providers;

/// <summary>
/// Maps a <see cref="LoggingRuntimeSinkDefinition"/> into the values
/// <see cref="MqttLogSink"/>'s constructor needs.
///
/// <para>
/// The Mqtt mmpform exposes two operator-facing fields today:
/// <list type="bullet">
///   <item><c>broker</c> — host[:port] of the MQTT broker (required).</item>
///   <item><c>topic</c> — MQTT topic, hierarchy supported.</item>
/// </list>
/// The bag-reader splits broker into host + port (default 1883) so
/// the sink constructor can take typed values. Older deployments
/// authored before the v2 bag existed populated the same values
/// through the definition's <c>Uri</c> and <c>Host</c> slots; this
/// mapper accepts either source.
/// </para>
///
/// <para>
/// Auth (username/password), MQTTS/TLS, and QoS — exposed through
/// the sink's code-first overload — are intentionally not surfaced
/// here. They live behind the mmpform's future expansion path
/// (Richard's audit BLOCKER row for Mqtt); reading them today would
/// let a hand-crafted bag bypass the form.
/// </para>
///
/// <para>
/// Reading order is bag-first: any populated
/// <see cref="LoggingRuntimeSinkDefinition.Properties"/> wins over
/// the legacy slot. That lets a freshly-written mmpform override an
/// older dashboard JSON without breaking deployments still carrying
/// values in the typed slots.
/// </para>
/// </summary>
internal static class MqttSinkRuntimeConfig
{
    private const string KeyBroker = "broker";
    private const string KeyTopic  = "topic";

    /// <summary>Default MQTT TCP port from MQTT-3.1.1 / MQTT-5.</summary>
    public const int DefaultBrokerPort = 1883;

    /// <summary>Default topic when the form is left blank — mirrors
    /// the mmpform's <c>init="herald/logs"</c> default.</summary>
    public const string DefaultTopic = "herald/logs";

    /// <summary>
    /// Resolved Mqtt sink config: typed broker host + port and the
    /// topic. <see cref="BrokerHost"/> is null when neither source
    /// supplies a broker — the provider rejects that case.
    /// </summary>
    public readonly record struct Resolved(
        string? BrokerHost,
        int BrokerPort,
        string Topic);

    public static Resolved From(LoggingRuntimeSinkDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var bag = definition.Properties;
        var brokerRaw = ReadString(bag, KeyBroker) ?? Nullify(definition.Uri);

        // Topic defaults to "herald/logs" when neither source supplies
        // one — matches the mmpform init and the sink's own ctor
        // default. Empty strings (form field left blank) follow the
        // same default to avoid publishing to an empty topic.
        var topicRaw = ReadString(bag, KeyTopic) ?? Nullify(definition.Host);
        var topic = string.IsNullOrWhiteSpace(topicRaw) ? DefaultTopic : topicRaw!;

        if (brokerRaw is null)
        {
            return new Resolved(BrokerHost: null, BrokerPort: DefaultBrokerPort, Topic: topic);
        }

        var (host, port) = ParseBroker(brokerRaw);
        return new Resolved(BrokerHost: host, BrokerPort: port, Topic: topic);
    }

    // Broker form is "host" or "host:port"; falls back to 1883 if the
    // port segment is missing or unparsable. Kept local because the
    // shape mirrors the prior provider behaviour and the mmpform
    // tooltip's "defaults to port 1883" wording.
    private static (string Host, int Port) ParseBroker(string raw)
    {
        var trimmed = raw.Trim();
        var colon = trimmed.IndexOf(':');
        if (colon < 0) return (trimmed, DefaultBrokerPort);

        var host = trimmed[..colon];
        var portText = trimmed[(colon + 1)..];
        return int.TryParse(portText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
            ? (host, port)
            : (host, DefaultBrokerPort);
    }

    private static string? ReadString(
        IReadOnlyDictionary<string, object?>? bag, string key)
    {
        if (bag is null) return null;
        if (!bag.TryGetValue(key, out var raw) || raw is null) return null;
        var text = raw.ToString();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static string? Nullify(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
