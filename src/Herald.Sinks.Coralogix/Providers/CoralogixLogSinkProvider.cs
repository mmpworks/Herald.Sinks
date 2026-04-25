// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using MMP.Herald;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Levels;
using MMP.Herald.Output.Rendering;
using MMP.Herald.Pipeline;
using MMP.Herald.Routing;

namespace Herald.Sinks.Coralogix.Providers;

public sealed class CoralogixLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "coralogix";
    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        throw new NotSupportedException(
            "Coralogix needs privateKey + applicationName + subsystemName which cannot ride safely through " +
            "a declarative sink definition. Construct CoralogixLogSink directly via the code-first ctor.");
    }
}
