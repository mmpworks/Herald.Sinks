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

namespace Herald.Sinks.Couchbase.Providers;

/// <summary>
/// Sink provider that instantiates <see cref="CouchbaseLogSink"/> from a
/// <see cref="LoggingRuntimeSinkDefinition"/>.
/// </summary>
/// <remarks>
/// <para>
/// Couchbase requires username + password + bucket; declarative provider
/// wire-up only takes uri + host. The provider intentionally throws so
/// callers know to use the code-first ctor — credentials cannot ride
/// safely through the runtime sink definition without a secret-store
/// integration we don't ship today.
/// </para>
/// </remarks>
public sealed class CouchbaseLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "couchbase";

    public string SinkKind => KindKey;
    public HeraldEdition MinimumEdition => HeraldEdition.Community;

    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        throw new NotSupportedException(
            "Couchbase requires credentials that cannot ride through a declarative sink definition. " +
            "Construct CouchbaseLogSink directly via the code-first ctor (connection string + username + password + bucket).");
    }
}
