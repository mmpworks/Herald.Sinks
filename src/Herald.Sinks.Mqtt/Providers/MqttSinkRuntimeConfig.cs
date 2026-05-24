// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Globalization;
using MQTTnet.Protocol;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Configuration.Sinks;

namespace Herald.Sinks.Mqtt.Providers;

/// <summary>
/// Maps a <see cref="LoggingRuntimeSinkDefinition"/> into the values
/// <see cref="MqttLogSink"/>'s constructor needs.
///
/// <para>
/// The Mqtt mmpform exposes five operator-facing fields:
/// <list type="bullet">
///   <item><c>broker</c> — host[:port] of the MQTT broker (required).</item>
///   <item><c>topic</c> — MQTT topic, hierarchy supported.</item>
///   <item><c>username</c> — optional MQTT username.</item>
///   <item><c>password</c> — optional MQTT password.</item>
///   <item><c>qos</c> — <c>at_most_once</c> / <c>at_least_once</c> /
///         <c>exactly_once</c> (default <c>at_most_once</c>).</item>
/// </list>
/// The bag-reader splits broker into host + port (default 1883) so the
/// sink constructor can take typed values. Older deployments authored
/// before the v2 bag existed populated <c>broker</c> and <c>topic</c>
/// through the definition's <c>Uri</c> and <c>Host</c> slots; this
/// mapper accepts either source. Auth and QoS have no legacy mapping.
/// </para>
///
/// <para>
/// MQTTS (TLS-wrapped MQTT) is intentionally not surfaced here — that
/// combination belongs to the Compliance-edition TLS sub-track.
/// </para>
///
/// <para>
/// Reading order is bag-first: any populated
/// <see cref="LoggingRuntimeSinkDefinition.Properties"/> wins over the
/// legacy slot. That lets a freshly-written mmpform override an older
/// dashboard JSON without breaking deployments still carrying values
/// in the typed slots.
/// </para>
/// </summary>
internal static class MqttSinkRuntimeConfig
{
    private const string KeyBroker   = "broker";
    private const string KeyTopic    = "topic";
    private const string KeyUsername = "username";
    private const string KeyPassword = "password";
    private const string KeyQos      = "qos";

    /// <summary>Default MQTT TCP port from MQTT-3.1.1 / MQTT-5.</summary>
    public const int DefaultBrokerPort = 1883;

    /// <summary>Default topic when the form is left blank — mirrors
    /// the mmpform's <c>init="herald/logs"</c> default.</summary>
    public const string DefaultTopic = "herald/logs";

    /// <summary>
    /// Resolved Mqtt sink config: typed broker host + port, the
    /// topic, optional credentials, and the QoS level. The QoS
    /// always carries a value — <c>AtMostOnce</c> when the bag is
    /// silent or carries an unrecognised string. <see cref="BrokerHost"/>
    /// is null when neither source supplies a broker; the provider
    /// rejects that case.
    /// </summary>
    public readonly record struct Resolved(
        string? BrokerHost,
        int BrokerPort,
        string Topic,
        string? Username,
        string? Password,
        MqttQualityOfServiceLevel Qos);

    public static Resolved From(LoggingRuntimeSinkDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var bag = definition.Properties;
        var brokerRaw = SinkPropertyBag.ReadString(bag, KeyBroker) ?? SinkPropertyBag.Nullify(definition.Uri);

        // Topic defaults to "herald/logs" when neither source supplies
        // one — matches the mmpform init and the sink's own ctor
        // default. Empty strings (form field left blank) follow the
        // same default to avoid publishing to an empty topic.
        var topicRaw = SinkPropertyBag.ReadString(bag, KeyTopic) ?? SinkPropertyBag.Nullify(definition.Host);
        var topic = string.IsNullOrWhiteSpace(topicRaw) ? DefaultTopic : topicRaw!;

        var username = SinkPropertyBag.ReadString(bag, KeyUsername);
        var password = SinkPropertyBag.ReadString(bag, KeyPassword);
        var qos = ParseQos(SinkPropertyBag.ReadString(bag, KeyQos));

        if (brokerRaw is null)
        {
            return new Resolved(
                BrokerHost: null, BrokerPort: DefaultBrokerPort, Topic: topic,
                Username: username, Password: password, Qos: qos);
        }

        var (host, port) = ParseBroker(brokerRaw);
        return new Resolved(
            BrokerHost: host, BrokerPort: port, Topic: topic,
            Username: username, Password: password, Qos: qos);
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

    // QoS vocabulary is taken from the mmpform tooltip text directly
    // so the form and the provider agree on the same spelling. Anything
    // else (including null) falls back to AtMostOnce — the safe choice
    // for a logging pipeline that prefers loss over backpressure.
    // Cannot use SinkPropertyBag.ReadEnum<MqttQualityOfServiceLevel>
    // because the mmpform vocabulary (at_most_once / at_least_once /
    // exactly_once) does not match the enum member names
    // (AtMostOnce / AtLeastOnce / ExactlyOnce) — case-insensitive
    // parsing alone cannot bridge the underscore-vs-PascalCase gap.
    // Keeping the local switch is per Richard's Pass-3 recipe step 4.
    private static MqttQualityOfServiceLevel ParseQos(string? raw) =>
        (raw ?? "").Trim().ToLowerInvariant() switch
        {
            "at_least_once" => MqttQualityOfServiceLevel.AtLeastOnce,
            "exactly_once"  => MqttQualityOfServiceLevel.ExactlyOnce,
            _               => MqttQualityOfServiceLevel.AtMostOnce,
        };
}
