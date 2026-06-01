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

namespace Herald.Sinks.ApplicationInsightsHttp.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="ApplicationInsightsLogSink"/>
/// from a <see cref="LoggingRuntimeSinkDefinition"/>. Bound to the
/// sink kind <c>application_insights_http</c>, which disambiguates
/// this HTTP-flavoured variant from the SDK-flavoured
/// <see cref="Herald.Sinks.ApplicationInsightsSdk"/> in JSON config
/// and dashboard catalogs.
/// </summary>
/// <remarks>
/// Wire-up conventions:
/// <list type="bullet">
///   <item><c>Uri</c> holds the AI connection string or bare instrumentation key.</item>
///   <item><c>Alias</c> doubles as the cloud role name (<c>ai.cloud.role</c>)
///     when set; when absent the tag is omitted.</item>
/// </list>
/// </remarks>
public sealed class ApplicationInsightsLogSinkProvider : BatchingSinkProviderBase
{
    public const string KindKey = "application_insights_http";
    public override string SinkKind => KindKey;
    public override HeraldEdition MinimumEdition => HeraldEdition.Community;

    public override ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);

        var sink = new ApplicationInsightsLogSink(
            connectionString: definition.Uri,
            roleName: definition.Alias);

        return BatchingLogSinkDecorator.Wrap(sink, BatchingOptions.From(definition));
    }
}
