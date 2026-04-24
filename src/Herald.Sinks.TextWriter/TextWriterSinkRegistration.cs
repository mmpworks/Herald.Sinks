// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.TextWriter.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.TextWriter;

/// <summary>
/// Opt-in registration helper. Registers the sink-kind so JSON config
/// that references <c>text_writer</c> fails with a clear message instead
/// of "unknown kind" — but the common path is to construct
/// <see cref="TextWriterLogSink"/> directly and pass it through
/// <c>WithCustomSinkProvider</c>.
/// </summary>
public static class TextWriterSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new TextWriterLogSinkProvider());
    }
}
