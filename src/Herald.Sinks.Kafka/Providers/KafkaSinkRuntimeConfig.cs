// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using MMP.Herald.Configuration.Runtime;

namespace Herald.Sinks.Kafka.Providers;

/// <summary>
/// Maps a <see cref="LoggingRuntimeSinkDefinition"/> into the values
/// <see cref="KafkaLogSink"/>'s connection-string constructor needs.
///
/// <para>
/// The Kafka mmpform exposes five fields:
/// <list type="bullet">
///   <item><c>bootstrap_servers</c> — comma-separated brokers (required).</item>
///   <item><c>topic</c> — destination topic (required).</item>
///   <item><c>sasl_mechanism</c> — <c>PLAIN</c> / <c>SCRAM-SHA-256</c> /
///         <c>SCRAM-SHA-512</c>. Empty disables SASL.</item>
///   <item><c>sasl_username</c> — required when sasl_mechanism is set.</item>
///   <item><c>sasl_password</c> — required when sasl_mechanism is set.</item>
/// </list>
/// SASL_SSL (TLS-wrapped SASL) is intentionally out of scope; that
/// belongs to the Compliance-edition TLS sub-track.
/// </para>
/// </summary>
internal static class KafkaSinkRuntimeConfig
{
    private const string KeyBootstrapServers = "bootstrap_servers";
    private const string KeyTopic            = "topic";
    private const string KeySaslMechanism    = "sasl_mechanism";
    private const string KeySaslUsername     = "sasl_username";
    private const string KeySaslPassword     = "sasl_password";

    /// <summary>
    /// Resolved Kafka sink config. Required fields stay nullable so
    /// the provider can fail with a single field-named
    /// ArgumentException.
    /// </summary>
    public readonly record struct Resolved(
        string? BootstrapServers,
        string? Topic,
        string? SaslMechanism,
        string? SaslUsername,
        string? SaslPassword);

    public static Resolved From(LoggingRuntimeSinkDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var bag = definition.Properties;
        return new Resolved(
            BootstrapServers: ReadString(bag, KeyBootstrapServers) ?? Nullify(definition.Uri),
            Topic:            ReadString(bag, KeyTopic)            ?? Nullify(definition.Host),
            SaslMechanism:    ReadString(bag, KeySaslMechanism),
            SaslUsername:     ReadString(bag, KeySaslUsername),
            SaslPassword:     ReadString(bag, KeySaslPassword));
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
