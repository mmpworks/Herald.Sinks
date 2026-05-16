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

namespace Herald.Sinks.XUnit.Providers;

/// <summary>
/// Sink provider intentionally limited to code-first construction —
/// an <c>ITestOutputHelper</c> is a per-test runtime value and cannot
/// come from JSON config. The provider throws on
/// <see cref="CreateSink"/> so accidental JSON registrations fail
/// loudly. Consumers instantiate <see cref="XUnitLogSink"/> in the
/// test ctor and register it via <c>WithCustomSinkProvider</c>.
/// </summary>
public sealed class XUnitLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "xunit";

    public string SinkKind => KindKey;
    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        throw new NotSupportedException(
            "XUnitLogSink has no JSON-config construction path because an " +
            "ITestOutputHelper is a per-test runtime value. Register the sink " +
            "in the test ctor via WithCustomSinkProvider(new XUnitLogSink(output)).");
    }
}
