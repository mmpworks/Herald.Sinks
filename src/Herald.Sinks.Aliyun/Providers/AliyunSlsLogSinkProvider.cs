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

namespace Herald.Sinks.Aliyun.Providers;

/// <summary>
/// Wire-up: Uri=endpoint host (e.g. https://cn-hangzhou.log.aliyuncs.com),
/// Host="{project}/{logstore}", Alias="{accessKeyId}:{accessKeySecret}".
/// </summary>
public sealed class AliyunSlsLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "aliyun_sls";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Host);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Alias);

        var slash = definition.Host.IndexOf('/');
        if (slash < 0)
        {
            throw new ArgumentException("Aliyun SLS sink Host must be 'project/logstore'.");
        }
        var project = definition.Host[..slash].Trim();
        var logstore = definition.Host[(slash + 1)..].Trim();

        var colon = definition.Alias.IndexOf(':');
        if (colon < 0)
        {
            throw new ArgumentException(
                "Aliyun SLS sink Alias must be 'accessKeyId:accessKeySecret'.");
        }
        var keyId = definition.Alias[..colon];
        var keySecret = definition.Alias[(colon + 1)..];

        return new AliyunSlsLogSink(
            endpoint: definition.Uri,
            projectName: project,
            logstoreName: logstore,
            accessKeyId: keyId,
            accessKeySecret: keySecret);
    }
}
