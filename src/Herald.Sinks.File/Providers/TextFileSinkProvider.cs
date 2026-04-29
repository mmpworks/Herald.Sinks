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
/// Sink provider for plain text or template-based log files.
/// </summary>
public sealed class TextFileSinkProvider : ILogSinkProvider
{
    private readonly ILogFilePathResolver? _pathResolver;

    public TextFileSinkProvider(ILogFilePathResolver? pathResolver = null)
    {
        _pathResolver = pathResolver;
    }

    public string SinkKind => KnownSinkKinds.TextFile;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ILogFormatter formatter = definition.OutputTemplate is not null
            ? new OutputTemplateFormatter(levelRegistry, definition.OutputTemplate)
            : new PlainTextFormatter(levelRegistry);

        return new WriterLogger(formatter, LogSinkFileWriterFactory.Create(definition, _pathResolver));
    }
}
