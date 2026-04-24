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

namespace Herald.Sinks.TextWriter.Providers;

/// <summary>
/// Sink provider that is intentionally limited to code-first
/// construction. There is no way to express an arbitrary
/// <see cref="System.IO.TextWriter"/> through JSON config — a writer is
/// a runtime value, not a declarative string. The provider throws on
/// <see cref="CreateSink"/> so accidental JSON registrations fail loudly
/// instead of silently doing nothing. Consumers register the sink via
/// <c>WithCustomSinkProvider</c> or by new-ing a
/// <see cref="TextWriterLogSink"/> directly.
/// </summary>
public sealed class TextWriterLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "text_writer";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        throw new NotSupportedException(
            "TextWriterLogSink has no JSON-config construction path because a " +
            "TextWriter is a runtime value. Register the sink directly via " +
            "WithCustomSinkProvider(new TextWriterLogSink(writer)).");
    }
}
