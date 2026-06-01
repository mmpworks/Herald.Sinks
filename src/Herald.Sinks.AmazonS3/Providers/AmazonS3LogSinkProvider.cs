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

namespace Herald.Sinks.AmazonS3.Providers;

public sealed class AmazonS3LogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "aws_s3";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);

        var sink = new AmazonS3LogSink(
            bucketName: definition.Uri,
            keyPrefix: string.IsNullOrWhiteSpace(definition.Alias) ? "logs" : definition.Alias,
            regionSystemName: definition.Host);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }
}
