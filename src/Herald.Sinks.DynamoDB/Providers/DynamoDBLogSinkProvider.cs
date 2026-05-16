// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using Amazon;
using MMP.Herald;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Levels;
using MMP.Herald.Output.Rendering;
using MMP.Herald.Pipeline;
using MMP.Herald.Routing;

namespace Herald.Sinks.DynamoDB.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="DynamoDBLogSink"/> from a
/// <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><c>Uri</c> → AWS region system name (required, e.g. <c>us-east-1</c>).</item>
///   <item><c>Host</c> → table name (required).</item>
/// </list>
/// </remarks>
public sealed class DynamoDBLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "dynamodb";

    public string SinkKind => KindKey;
    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Host);

        var region = RegionEndpoint.GetBySystemName(definition.Uri);
        return new DynamoDBLogSink(definition.Host, region);
    }
}
