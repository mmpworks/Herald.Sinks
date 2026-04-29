// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using MMP.Herald;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Formatting;
using MMP.Herald.Levels;
using MMP.Herald.Output.Rendering;
using MMP.Herald.Output.Writers;
using MMP.Herald.Pipeline;
using MMP.Herald.Routing;
using MMP.Herald.Services;

namespace Herald.Sinks.File.Providers;

/// <summary>
/// Sink provider for NDJSON log files.
/// </summary>
public sealed class JsonFileSinkProvider : ILogSinkProvider
{
    private readonly ILogFilePathResolver? _pathResolver;

    public JsonFileSinkProvider(ILogFilePathResolver? pathResolver = null)
    {
        _pathResolver = pathResolver;
    }

    public string SinkKind => KnownSinkKinds.JsonFile;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        return new WriterLogger(
            new JsonFormatter(levelRegistry),
            LogSinkFileWriterFactory.Create(definition, _pathResolver));
    }
}
