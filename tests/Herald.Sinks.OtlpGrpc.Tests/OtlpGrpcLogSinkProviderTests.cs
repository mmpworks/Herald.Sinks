// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using FluentAssertions;
using Herald.Sinks.OtlpGrpc.Providers;
using Xunit;

namespace Herald.Sinks.OtlpGrpc.Tests;

public sealed class OtlpGrpcLogSinkProviderTests
{
    [Fact] public void KindKey_constant_is_otlp_grpc() =>
        OtlpGrpcLogSinkProvider.KindKey.Should().Be("otlp_grpc");

    [Fact] public void Provider_SinkKind_matches_KindKey() =>
        new OtlpGrpcLogSinkProvider().SinkKind.Should().Be(OtlpGrpcLogSinkProvider.KindKey);

    [Fact] public void Provider_throws_on_null_definition() =>
        ((Action)(() => new OtlpGrpcLogSinkProvider().CreateSink(
            definition: null!, levelRegistry: null!, transformerRegistry: null!)))
            .Should().Throw<ArgumentNullException>();
}
