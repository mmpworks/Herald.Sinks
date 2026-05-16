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

namespace Herald.Sinks.MSSqlServer.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="MSSqlServerLogSink"/> from a
/// <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// Wire-up:
/// <list type="bullet">
///   <item><c>Uri</c> → connection string (required).</item>
///   <item><c>Host</c> → fully-qualified table, e.g. <c>dbo.Logs</c>. Default
///   <c>dbo.Logs</c>. A single token is treated as a table name under the
///   default schema.</item>
///   <item><c>Alias</c> → when equal to <c>auto-create</c>, the sink will
///   create the table on first write.</item>
/// </list>
/// Custom column names require a code-first ctor with
/// <see cref="MSSqlServerColumnOptions"/>.
/// </remarks>
public sealed class MSSqlServerLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "mssql";

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

        return new MSSqlServerLogSink(
            connectionString: definition.Uri,
            tableName: table,
            schemaName: schema,
            autoCreateTable: autoCreate);
    }

    private static (string Schema, string Table) ParseHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return ("dbo", "Logs");

        var dot = host.IndexOf('.');
        if (dot < 0) return ("dbo", host);

        var schema = host[..dot].Trim();
        var table = host[(dot + 1)..].Trim();
        if (schema.Length == 0) schema = "dbo";
        if (table.Length == 0) table = "Logs";
        return (schema, table);
    }
}
