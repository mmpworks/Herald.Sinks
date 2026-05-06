// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Output.Writers;
using MMP.RollingFiles;

namespace Herald.Sinks.File.Providers;

/// <summary>
/// Shared helper for creating file writers from sink definitions.
/// Used by both <see cref="TextFileSinkProvider"/> and
/// <see cref="JsonFileSinkProvider"/> to avoid duplication.
///
/// <para>
/// v2 sinks (definitions carrying a <c>Properties</c> bag) route
/// every write through <see cref="FilesManager"/> from the
/// <c>MMP.RollingFiles</c> library; the shim
/// <see cref="FilesManagerLineWriter"/> keeps the in-Core
/// <see cref="ILineWriter"/> shape so the sink providers stay
/// unchanged. Phase 1 of the migration: project reference today,
/// package reference once the standalone repo publishes.
/// </para>
///
/// <para>
/// Legacy sinks (no <c>Properties</c> bag, just the typed <c>Path</c>
/// + <c>RollingPolicy</c> fields) still construct the old in-Core
/// writers via <see cref="FileSinkRuntimeConfig"/>. Those writers
/// stay in Core during the migration window; once every caller moves
/// to the v2 bag-driven path, this branch and the in-Core writers can
/// retire together.
/// </para>
/// </summary>
internal static class LogSinkFileWriterFactory
{
    public static ILineWriter Create(
        LoggingRuntimeSinkDefinition definition,
        ILogFilePathResolver? pathResolver = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        // v2 path: any definition with a populated Properties bag uses
        // MMP.RollingFiles. Bag interpretation lives in the
        // mapper so this factory stays a one-line dispatch. The shim
        // takes the policy (not a constructed manager) so filesystem
        // IO is deferred to the first WriteLine, matching the legacy
        // RollingFileLineWriter timing.
        if (definition.Properties is { Count: > 0 })
        {
            var policy = FilesManagerPolicyMapper.From(definition);
            return new FilesManagerLineWriter(policy);
        }

        // Legacy path: callers that have not migrated to the property
        // bag still produce typed Path + RollingPolicy. Route them
        // through the old in-Core writers via FileSinkRuntimeConfig
        // (which collapses to (Path, Rolling) regardless of source).
        var resolved = FileSinkRuntimeConfig.From(definition);
        return resolved.Rolling is not null
            ? new RollingFileLineWriter(resolved.Path, resolved.Rolling, pathResolver)
            : new FileLineWriter(resolved.Path, pathResolver);
    }
}
