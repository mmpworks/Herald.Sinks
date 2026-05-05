// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Grpc.Core;
using Grpc.Net.Client;
using Herald.Sinks.Otlp;
using MMP.Herald;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Pipeline;

namespace Herald.Sinks.OtlpGrpc;

/// <summary>
/// Sink that exports log events to an OpenTelemetry collector over
/// OTLP/gRPC. Calls
/// <c>opentelemetry.proto.collector.logs.v1.LogsService/Export</c>
/// with an ExportLogsServiceRequest payload produced by the shared
/// <see cref="OtlpProtobufLogSerializer"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>No proto codegen.</b> The sink reuses the hand-rolled protobuf
/// writer from <see cref="Herald.Sinks.Otlp"/> for the request body
/// and uses raw byte marshallers on the gRPC <see cref="Method{TRequest,TResponse}"/>.
/// The response (<c>ExportLogsServiceResponse</c>) is read but not
/// parsed — a successful gRPC status is treated as success, matching
/// the spec's "empty partial_success means total success" convention.
/// </para>
/// <para>
/// <b>Channel lifetime.</b> The <see cref="GrpcChannel"/> is owned by
/// the sink and disposed with it. Each call uses the channel's
/// connection-pooled HTTP/2 socket; gRPC's keepalive defaults apply.
/// </para>
/// <para>
/// <b>Thread safety.</b> <see cref="GrpcChannel"/> is thread-safe;
/// concurrent <c>Log</c> / <c>LogBatch</c> calls share the channel.
/// </para>
/// </remarks>
public sealed class OtlpGrpcLogSink : ILogger, IBatchedLogSink, IDisposable
{
    private const string ServiceName = "opentelemetry.proto.collector.logs.v1.LogsService";
    private const string MethodName = "Export";

    private static readonly Method<byte[], byte[]> ExportMethod = new(
        type: MethodType.Unary,
        serviceName: ServiceName,
        name: MethodName,
        requestMarshaller: Marshallers.Create(b => b, b => b),
        responseMarshaller: Marshallers.Create(b => b, b => b));

    private readonly GrpcChannel _channel;
    private readonly OtlpProtobufLogSerializer _serializer;
    private readonly TimeSpan _callDeadline;

    public OtlpGrpcLogSink(
        string endpoint,
        ILogLevelRegistry levelRegistry,
        IReadOnlyDictionary<string, string>? resourceAttributes = null,
        TimeSpan? callDeadline = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentNullException.ThrowIfNull(levelRegistry);

        _channel = GrpcChannel.ForAddress(endpoint);
        _serializer = new OtlpProtobufLogSerializer(levelRegistry, resourceAttributes);
        _callDeadline = callDeadline ?? TimeSpan.FromSeconds(30);
    }

    public void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        LogBatch([logEvent]);
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        var payload = _serializer.SerializeBatch(events);

        var invoker = _channel.CreateCallInvoker();
        var options = new CallOptions(deadline: DateTime.UtcNow.Add(_callDeadline));

        using var call = invoker.AsyncUnaryCall(ExportMethod, host: null, options, payload);
        // Block on completion; ILogger.Log / IBatchedLogSink.LogBatch are
        // sync. The HTTP-OTLP sibling (OtlpProtobufLogSink) bridges the
        // same way via HttpClient.Send.
        _ = call.ResponseAsync.GetAwaiter().GetResult();
    }

    public void Dispose() => _channel.Dispose();
}
