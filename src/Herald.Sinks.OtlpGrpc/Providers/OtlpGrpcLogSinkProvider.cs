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

namespace Herald.Sinks.OtlpGrpc.Providers;

/// <summary>
/// Sink provider for OTLP/gRPC log export. Posts log events to an
/// OpenTelemetry collector's gRPC endpoint.
/// </summary>
/// <remarks>
/// Wire-up:
/// <list type="bullet">
///   <item><c>Uri</c> → collector address (required), e.g. <c>http://otel-collector:4317</c>
///   or <c>https://otel-collector:4317</c>. Standard OTLP/gRPC port is 4317.</item>
/// </list>
/// Resource attributes and call deadline are not configurable through
/// the runtime-definition surface today — construct
/// <see cref="OtlpGrpcLogSink"/> directly and register via
/// <c>WithCustomSinkProvider</c> if you need either.
/// </remarks>
public sealed class OtlpGrpcLogSinkProvider : ILogSinkProvider
{
    public const string KindKey = "otlp_grpc";

    public string SinkKind => KindKey;
    public ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Uri);
        return new OtlpGrpcLogSink(definition.Uri, levelRegistry);
    }
}
