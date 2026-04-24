// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Herald.Sinks.Otlp;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Tests.Helpers;
using Xunit;

namespace Herald.Sinks.Otlp.Tests;

public sealed class ProtobufFileLogSinkTests
{
    private readonly ILogLevelRegistry _registry = RegistryHelper.CreateDefault();

    [Fact]
    public void Null_path_throws() {
        var act = () => new ProtobufFileLogSink(null!, _registry);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Empty_path_throws() {
        var act = () => new ProtobufFileLogSink("", _registry);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Null_registry_throws() {
        var path = Path.GetTempFileName();
        try
        {
            var act = () => new ProtobufFileLogSink(path, null!);
            act.Should().Throw<ArgumentNullException>();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Log_writes_length_delimited_record() {
        var path = Path.GetTempFileName();
        try
        {
            using (var sink = new ProtobufFileLogSink(path, _registry))
            {
                var evt = LogEventBuilder.Create()
                    .WithLevel(KnownLogLevels.Info)
                    .WithMessage("test record")
                    .Build();

                sink.Log(evt);
            }

            var bytes = File.ReadAllBytes(path);

            // Must have at least 4-byte length prefix plus some payload
            bytes.Length.Should().BeGreaterThan(4);

            // First 4 bytes are little-endian length of the payload
            var payloadLength = BitConverter.ToInt32(bytes, 0);
            payloadLength.Should().BeGreaterThan(0);
            payloadLength.Should().Be(bytes.Length - 4);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LogBatch_writes_multiple_records() {
        var path = Path.GetTempFileName();
        try
        {
            long singleSize;

            // Write a single event to measure its size
            using (var sink = new ProtobufFileLogSink(path, _registry))
            {
                sink.Log(LogEventBuilder.Create()
                    .WithLevel(KnownLogLevels.Info)
                    .WithMessage("single")
                    .Build());
            }

            singleSize = new FileInfo(path).Length;

            // Overwrite with a batch of 3
            File.Delete(path);
            using (var sink = new ProtobufFileLogSink(path, _registry))
            {
                var events = new List<LogEvent>
                {
                    LogEventBuilder.Create().WithMessage("batch-1").Build(),
                    LogEventBuilder.Create().WithMessage("batch-2").Build(),
                    LogEventBuilder.Create().WithMessage("batch-3").Build()
                };

                sink.LogBatch(events);
            }

            new FileInfo(path).Length.Should().BeGreaterThan(singleSize);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Empty_batch_writes_nothing() {
        var path = Path.GetTempFileName();
        try
        {
            using (var sink = new ProtobufFileLogSink(path, _registry))
            {
                sink.LogBatch(new List<LogEvent>());
            }

            // Temp file starts empty; batch of zero should leave it that way
            new FileInfo(path).Length.Should().Be(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Dispose_allows_file_access() {
        var path = Path.GetTempFileName();
        try
        {
            var sink = new ProtobufFileLogSink(path, _registry);
            sink.Log(LogEventBuilder.Create().WithMessage("dispose test").Build());
            sink.Dispose();

            // After dispose, the file should exist and be fully readable
            File.Exists(path).Should().BeTrue();
            var bytes = File.ReadAllBytes(path);
            bytes.Length.Should().BeGreaterThan(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Rolling_creates_new_file_when_size_exceeded() {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var basePath = Path.Combine(dir, "rolling.pb");

        try
        {
            // Use a very small max to force rolling after the first event
            using (var sink = new ProtobufFileLogSink(basePath, _registry, maxFileSizeBytes: 50))
            {
                for (var i = 0; i < 5; i++)
                {
                    sink.Log(LogEventBuilder.Create()
                        .WithMessage($"rolling event {i}")
                        .Build());
                }
            }

            // The original file plus at least one rolled file should exist
            var files = Directory.GetFiles(dir, "rolling*");
            files.Length.Should().BeGreaterThan(1);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Double_dispose_does_not_throw() {
        var path = Path.GetTempFileName();
        try
        {
            var sink = new ProtobufFileLogSink(path, _registry);
            sink.Dispose();

            var act = () => sink.Dispose();
            act.Should().NotThrow();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
