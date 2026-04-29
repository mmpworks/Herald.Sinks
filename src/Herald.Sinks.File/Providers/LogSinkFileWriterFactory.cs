// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Output.Writers;

namespace Herald.Sinks.File.Providers;

/// <summary>
/// Shared helper for creating file writers from sink definitions.
/// Used by both TextFileSinkProvider and JsonFileSinkProvider to avoid duplication.
/// </summary>
internal static class LogSinkFileWriterFactory
{
    public static ILineWriter Create(
        LoggingRuntimeSinkDefinition definition,
        ILogFilePathResolver? pathResolver = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Path);

        return definition.RollingPolicy is not null
            ? new RollingFileLineWriter(definition.Path, definition.RollingPolicy, pathResolver)
            : new FileLineWriter(definition.Path, pathResolver);
    }
}
