// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using FluentAssertions;
using Herald.Sinks.File.Providers;
using MMP.Herald.Sinks;
using Xunit;

namespace Herald.Sinks.File.Tests;

/// <summary>
/// Locks in two shape invariants on <see cref="FileWriterLogger"/>:
///
/// <list type="number">
///   <item>It inherits <see cref="HeraldSinkBase"/> so file sinks ride
///   the same kernel fast path every other sink rides.</item>
///   <item>It carries <see cref="INetworkSink"/> so the pipeline-
///   construction advisory catches a file sink wired without
///   <c>WithAsync()</c> — disk I/O at delivery time blocks the producer
///   thread the same way an HTTP POST blocks it.</item>
/// </list>
///
/// <para>
/// The marker lives on <see cref="FileWriterLogger"/> rather than the
/// upstream <c>WriterLogger</c> because that class is shared with
/// console / in-memory writers where <see cref="INetworkSink"/> does
/// not apply. A future "simplify back to WriterLogger" refactor would
/// silently drop the marker; this test fails the build if that happens.
/// </para>
/// </summary>
public sealed class FileWriterLoggerTaggingTests
{
    [Fact]
    public void FileWriterLogger_is_INetworkSink() =>
        typeof(INetworkSink).IsAssignableFrom(typeof(FileWriterLogger))
            .Should().BeTrue(
                "the marker tells the pipeline-construction advisory that " +
                "file sinks do potentially-blocking disk I/O at delivery time");

    [Fact]
    public void FileWriterLogger_inherits_HeraldSinkBase() =>
        typeof(FileWriterLogger).IsSubclassOf(typeof(HeraldSinkBase))
            .Should().BeTrue(
                "file sinks ride the same HeraldSinkBase kernel fast path as the rest");
}
