// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using MMP.Herald;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Levels;
using MMP.Herald.Output.Rendering;
using MMP.Herald.Pipeline;
using MMP.Herald.Routing;

namespace Herald.Sinks.Seq.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="SeqLogSink"/> from a
/// <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// Wire-up conventions:
/// <list type="bullet">
///   <item><c>Uri</c> holds the Seq server root URL
///     (e.g. <c>http://localhost:5341</c>).</item>
///   <item><c>Alias</c> doubles as the API key when set (maps to the
///     <c>X-Seq-ApiKey</c> header).</item>
/// </list>
/// </remarks>
public sealed class SeqLogSinkProvider : ILogSinkProvider
{
    /// <summary>
    /// The sink-kind string that identifies this provider in JSON config.
    /// </summary>
    public const string KindKey = "seq";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);

        return new SeqLogSink(
            serverUrl: definition.Uri,
            apiKey: definition.Alias);
    }
}
