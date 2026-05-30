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

namespace Herald.Sinks.Coralogix.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="CoralogixLogSink"/> from
/// a <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// <para>
/// Configuration sources, in priority order:
/// <list type="bullet">
///   <item><b>v2 property bag</b> (<c>configuration-coralogix.mmpform</c> → <see cref="LoggingRuntimeSinkDefinition.Properties"/>):
///     <list type="bullet">
///       <item><c>endpoint</c> — bulk-logs intake URL.</item>
///       <item><c>private_key</c> — Coralogix private key (required).</item>
///       <item><c>application_name</c> — logical application name (required).</item>
///       <item><c>subsystem_name</c> — component within the application (required).</item>
///     </list>
///   </item>
///   <item><b>Legacy slots</b> (forward-compatibility — Coralogix had
///         no legacy JSON-config path before the v2 bag landed):
///     <list type="bullet">
///       <item><see cref="LoggingRuntimeSinkDefinition.Uri"/> ↔ endpoint.</item>
///       <item><see cref="LoggingRuntimeSinkDefinition.Alias"/> ↔ private_key.</item>
///       <item><see cref="LoggingRuntimeSinkDefinition.Host"/> ↔ application_name.</item>
///     </list>
///   </item>
/// </list>
/// The prior provider threw <see cref="NotSupportedException"/> because
/// the credential surface could not ride through the three legacy
/// string slots safely. The v2 bag fixes that — credentials live in
/// their own named field with secret-store handling on the form side.
/// </para>
/// </remarks>
public sealed class CoralogixLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "coralogix";
    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var resolved = CoralogixSinkRuntimeConfig.From(definition);

        if (string.IsNullOrWhiteSpace(resolved.PrivateKey))
        {
            throw new ArgumentException(
                $"Coralogix sink '{definition.Name}' is missing the required 'private_key' property " +
                $"(or legacy Alias). Set it in configuration-coralogix.mmpform.",
                nameof(definition));
        }
        if (string.IsNullOrWhiteSpace(resolved.ApplicationName))
        {
            throw new ArgumentException(
                $"Coralogix sink '{definition.Name}' is missing the required 'application_name' property " +
                $"(or legacy Host). Set it in configuration-coralogix.mmpform.",
                nameof(definition));
        }
        if (string.IsNullOrWhiteSpace(resolved.SubsystemName))
        {
            throw new ArgumentException(
                $"Coralogix sink '{definition.Name}' is missing the required 'subsystem_name' property. " +
                $"Set it in configuration-coralogix.mmpform.",
                nameof(definition));
        }

        return new CoralogixLogSink(
            privateKey: resolved.PrivateKey!,
            applicationName: resolved.ApplicationName!,
            subsystemName: resolved.SubsystemName!,
            endpoint: resolved.Endpoint,
            preserveProperties: resolved.PreserveProperties);
    }
}
