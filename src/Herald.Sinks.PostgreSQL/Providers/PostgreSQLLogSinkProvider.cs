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

namespace Herald.Sinks.PostgreSQL.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="PostgreSQLLogSink"/> from
/// a <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// Wire-up:
/// <list type="bullet">
///   <item><c>Uri</c> → connection string (required).</item>
///   <item><c>Host</c> → <c>schema.table</c>, default <c>public.logs</c>.</item>
///   <item><c>Alias</c> → <c>auto-create</c> to enable CREATE TABLE IF
///   NOT EXISTS DDL on first write.</item>
/// </list>
/// </remarks>
public sealed class PostgreSQLLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "postgresql";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);

        var (schema, table) = ParseHost(definition.Host);
        var autoCreate = string.Equals(definition.Alias, "auto-create", StringComparison.OrdinalIgnoreCase);

        return new PostgreSQLLogSink(
            connectionString: definition.Uri,
            tableName: table,
            schemaName: schema,
            autoCreateTable: autoCreate);
    }

    private static (string Schema, string Table) ParseHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return ("public", "logs");

        var dot = host.IndexOf('.');
        if (dot < 0) return ("public", host);

        var schema = host[..dot].Trim();
        var table = host[(dot + 1)..].Trim();
        if (schema.Length == 0) schema = "public";
        if (table.Length == 0) table = "logs";
        return (schema, table);
    }
}
