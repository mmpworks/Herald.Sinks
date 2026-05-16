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

namespace Herald.Sinks.Cassandra.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="CassandraLogSink"/> from a
/// <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><c>Uri</c> → comma-separated contact points (required).</item>
///   <item><c>Host</c> → <c>keyspace.table</c> (default <c>herald.herald_logs</c>).</item>
/// </list>
/// </remarks>
public sealed class CassandraLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "cassandra";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);

        var contactPoints = definition.Uri.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var (keyspace, table) = ParseHost(definition.Host);
        return new CassandraLogSink(contactPoints, keyspace, table);
    }

    private static (string Keyspace, string Table) ParseHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return ("herald", "herald_logs");
        var dot = host.IndexOf('.');
        if (dot < 0) return ("herald", host);
        var keyspace = host[..dot].Trim();
        var table = host[(dot + 1)..].Trim();
        if (keyspace.Length == 0) keyspace = "herald";
        if (table.Length == 0) table = "herald_logs";
        return (keyspace, table);
    }
}
