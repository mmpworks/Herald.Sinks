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
using MMP.Herald.Sinks.Batching;

namespace Herald.Sinks.MySQL.Providers;

public sealed class MySQLLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "mysql";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);

        var autoCreate = string.Equals(definition.Alias, "auto-create", StringComparison.OrdinalIgnoreCase);

        var sink = new MySQLLogSink(
            connectionString: definition.Uri,
            tableName: string.IsNullOrWhiteSpace(definition.Host) ? "logs" : definition.Host,
            autoCreateTable: autoCreate);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }
}
