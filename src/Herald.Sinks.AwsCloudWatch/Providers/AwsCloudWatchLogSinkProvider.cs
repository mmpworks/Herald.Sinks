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

namespace Herald.Sinks.AwsCloudWatch.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="AwsCloudWatchLogSink"/>
/// from a <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// Wire-up:
/// <list type="bullet">
///   <item><c>Uri</c> → <c>logGroup/logStream</c> (required).</item>
///   <item><c>Host</c> → AWS region system name (e.g. <c>us-east-1</c>).
///   Optional — falls back to the default credential resolution chain's
///   default region.</item>
///   <item><c>Alias</c> → set to <c>auto-create</c> to enable both
///   log-group and log-stream auto-creation on first write.</item>
/// </list>
/// </remarks>
public sealed class AwsCloudWatchLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "aws_cloudwatch";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);

        var (group, stream) = ParseUri(definition.Uri);
        var autoCreate = string.Equals(definition.Alias, "auto-create", StringComparison.OrdinalIgnoreCase);

        return new AwsCloudWatchLogSink(
            logGroupName: group,
            logStreamName: stream,
            regionSystemName: definition.Host,
            autoCreateLogGroup: autoCreate,
            autoCreateLogStream: autoCreate);
    }

    private static (string Group, string Stream) ParseUri(string uri)
    {
        var slash = uri.IndexOf('/');
        if (slash < 0)
        {
            throw new ArgumentException(
                "AwsCloudWatch sink Uri must be in 'logGroup/logStream' form.",
                nameof(uri));
        }

        var group = uri[..slash].Trim();
        var stream = uri[(slash + 1)..].Trim();
        if (group.Length == 0 || stream.Length == 0)
        {
            throw new ArgumentException(
                "AwsCloudWatch sink Uri must have both logGroup and logStream non-empty.",
                nameof(uri));
        }
        return (group, stream);
    }
}
