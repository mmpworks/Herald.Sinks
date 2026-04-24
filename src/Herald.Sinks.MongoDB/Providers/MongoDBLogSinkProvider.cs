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

namespace Herald.Sinks.MongoDB.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="MongoDBLogSink"/> from a
/// <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// Wire-up:
/// <list type="bullet">
///   <item><c>Uri</c> → MongoDB connection string (required).</item>
///   <item><c>Host</c> → <c>database.collection</c>, default
///   <c>herald.logs</c>. Single token is taken as the collection
///   under the <c>herald</c> database.</item>
/// </list>
/// </remarks>
public sealed class MongoDBLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "mongodb";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);

        var (database, collection) = ParseHost(definition.Host);
        return new MongoDBLogSink(definition.Uri, database, collection);
    }

    private static (string Database, string Collection) ParseHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return ("herald", "logs");

        var dot = host.IndexOf('.');
        if (dot < 0) return ("herald", host);

        var database = host[..dot].Trim();
        var collection = host[(dot + 1)..].Trim();
        if (database.Length == 0) database = "herald";
        if (collection.Length == 0) collection = "logs";
        return (database, collection);
    }
}
