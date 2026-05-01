// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Output.Writers;

namespace Herald.Sinks.File.Providers;

/// <summary>
/// Shared helper for creating file writers from sink definitions.
/// Used by both <see cref="TextFileSinkProvider"/> and
/// <see cref="JsonFileSinkProvider"/> to avoid duplication.
///
/// <para>v2 contract: when the definition carries a <c>Properties</c>
/// bag, this factory pulls the path and rolling policy from the bag
/// via <see cref="FileSinkRuntimeConfig"/>. The legacy <c>Path</c> +
/// <c>RollingPolicy</c> fields remain the source of truth for callers
/// that have not migrated, so the build still passes for definitions
/// constructed by the older code paths.</para>
/// </summary>
internal static class LogSinkFileWriterFactory
{
    public static ILineWriter Create(
        LoggingRuntimeSinkDefinition definition,
        ILogFilePathResolver? pathResolver = null)
    {
        var resolved = FileSinkRuntimeConfig.From(definition);

        return resolved.Rolling is not null
            ? new RollingFileLineWriter(resolved.Path, resolved.Rolling, pathResolver)
            : new FileLineWriter(resolved.Path, pathResolver);
    }
}
