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

namespace Herald.Sinks.AzureCosmosDB.Providers;

public sealed class AzureCosmosDbLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "azure_cosmosdb";

    public string SinkKind => KindKey;
    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Alias);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Host);

        // Host is "database/container", Alias is the auth key.
        var slash = definition.Host.IndexOf('/');
        if (slash < 0)
        {
            throw new ArgumentException(
                "AzureCosmosDB sink Host must be in 'database/container' form.",
                nameof(definition));
        }
        var db = definition.Host[..slash].Trim();
        var container = definition.Host[(slash + 1)..].Trim();

        return new AzureCosmosDbLogSink(
            endpoint: definition.Uri,
            authKey: definition.Alias,
            databaseName: db,
            containerName: container);
    }
}
