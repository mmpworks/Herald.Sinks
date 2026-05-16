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

namespace Herald.Sinks.Email.Providers;

/// <summary>
/// Sink provider intentionally limited to code-first construction —
/// the SMTP credential surface is more configuration than fits a
/// simple <see cref="LoggingRuntimeSinkDefinition"/>. Throws on
/// CreateSink so accidental JSON registrations fail loudly. Consumers
/// instantiate <see cref="EmailLogSink"/> in their composition root
/// and register it via <c>WithCustomSinkProvider</c>.
/// </summary>
public sealed class EmailLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "email";

    public string SinkKind => KindKey;
    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        throw new NotSupportedException(
            "EmailLogSink has no JSON-config construction path because the SMTP " +
            "credential and recipient surface is too rich for a single-line config. " +
            "Construct EmailLogSink directly and register it via " +
            "WithCustomSinkProvider(new EmailLogSink(...)).");
    }
}
