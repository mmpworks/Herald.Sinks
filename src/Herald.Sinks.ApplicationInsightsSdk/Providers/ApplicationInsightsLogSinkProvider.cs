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

namespace Herald.Sinks.ApplicationInsightsSdk.Providers;

/// <summary>
/// Provider for the SDK-flavoured Application Insights sink. The kind
/// string <c>application_insights_sdk</c> disambiguates this from the
/// HTTP-flavoured variant (<see cref="Herald.Sinks.ApplicationInsightsHttp" />)
/// in JSON config and in dashboard catalog listings.
/// </summary>
public sealed class ApplicationInsightsLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "application_insights_sdk";
    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);
        return new ApplicationInsightsLogSink(definition.Uri);
    }
}
