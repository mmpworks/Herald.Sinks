// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Herald.Sinks.File;
using MMP.Herald.Addons.ManagementApi;
using MMP.Herald.Configuration;
using MMP.Herald.Quick;
using MMP.Herald.Services;
using Xunit;

namespace Herald.Sinks.File.Tests;

/// <summary>
/// Closes the loop on the v2 sink-config flow the dashboard depends on.
/// A commit travels in via <see cref="HeraldManagementApi.CommitFull"/>
/// carrying a <c>properties: {…}</c> sub-object, the management API
/// persists the running pipeline to a JSON file, and a fresh builder
/// reads that file back through <see cref="HeraldManagementApi.RestoreBuilderFromConfig"/>
/// — the sequence a server reboot follows.
///
/// <para>Every key the operator set in the bag has to survive both
/// hops or a reboot would silently drop the configuration. These tests
/// pin that invariant.</para>
/// </summary>
public sealed class TextFileSinkV2RoundTripTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public TextFileSinkV2RoundTripTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"herald_v2_rt_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "pipeline.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private string TempDirAsForward => _tempDir.Replace('\\', '/');

    // Set up a builder whose committed state will land at _configPath.
    // Mirrors the host wiring: the registry hands the management API
    // a ConfigPath, and PersistConfig writes through that handle on
    // every successful commit.
    private (QuickLogBuilder Builder, HeraldManagementApi Api) CreatePipeline()
    {
        var builder = QuickLogBuilder.Create()
            .WithFileSinkProviders()
            .WithFileSink(Path.Combine(_tempDir, "initial.log"))
            .WithMinimumLevel("info")
            .WithHotReload();
        var result = builder.BuildAndCommit();
        var api = new HeraldManagementApi(builder, result) { ConfigPath = _configPath };
        return (builder, api);
    }

    [Fact]
    public void CommitFull_accepts_v2_properties_envelope_and_applies_to_builder()
    {
        // The shape the dashboard sends after the configContract gate
        // flips a sink to v2: every value lives inside a `properties`
        // sub-object on the sink's config field; minLevel still rides
        // on the outer envelope as core-managed metadata.
        var (builder, api) = CreatePipeline();
        var json = $$"""
            {
              "sinks": [
                {
                  "sinkId": "text_file",
                  "kind":   "text_file",
                  "config": {
                    "minLevel": "warn",
                    "properties": {
                      "logDirectory":       "{{TempDirAsForward}}",
                      "logFileTemplate":    "v2-commit",
                      "logExtension":       "log",
                      "logOutputType":      "text",
                      "rollingLogsEnabled": true,
                      "rollingInterval":    "daily",
                      "maxFileSize":        "16MB",
                      "maxRetainedFiles":   9,
                      "namePattern":        "yyyy-MM-dd",
                      "totalSizeCap":       "256MB",
                      "retentionDays":      14
                    }
                  }
                }
              ]
            }
            """;

        api.CommitFull(json).Success.Should().BeTrue();

        var inspection = builder.Inspect();
        inspection.HasFileSink.Should().BeTrue();
        inspection.FilePath.Should().Be($"{TempDirAsForward}/v2-commit.log");
        inspection.FileMinLevel.Should().Be("warn");
        inspection.HasFileRolling.Should().BeTrue();
        inspection.FileRollingInterval.Should().Be("daily");
        inspection.FileMaxBytes.Should().Be(16L * 1024 * 1024);
        inspection.FileMaxRetainedFiles.Should().Be(9);
        inspection.FileNamePattern.Should().Be("yyyy-MM-dd");
        inspection.TotalSizeCapBytes.Should().Be(256L * 1024 * 1024);
        inspection.RetentionDays.Should().Be(14);
    }

    [Fact]
    public void CommitFull_persists_v2_properties_to_disk_and_a_fresh_builder_restores_them()
    {
        // The reboot scenario the user named: commit lands, JSON gets
        // written, the running process exits, and a brand-new
        // QuickLogBuilder reads the same file back. Every key the
        // operator chose must be on the rebuilt builder.
        var (originalBuilder, api) = CreatePipeline();
        var json = $$"""
            {
              "sinks": [
                {
                  "sinkId": "text_file",
                  "kind":   "text_file",
                  "config": {
                    "minLevel": "info",
                    "properties": {
                      "logDirectory":       "{{TempDirAsForward}}",
                      "logFileTemplate":    "reboot-survivor",
                      "logExtension":       "log",
                      "logOutputType":      "text",
                      "rollingLogsEnabled": true,
                      "rollingInterval":    "hourly",
                      "maxFileSize":        "32MB",
                      "maxRetainedFiles":   24,
                      "namePattern":        "_yyyyMMdd_HH",
                      "totalSizeCap":       "1GB",
                      "retentionDays":      30
                    }
                  }
                }
              ]
            }
            """;

        api.CommitFull(json).Success.Should().BeTrue();

        // Disk handoff — the management API wrote the JSON because
        // ConfigPath was set on construction.
        System.IO.File.Exists(_configPath).Should().BeTrue("CommitFull must persist when ConfigPath is set");
        var saved = System.IO.File.ReadAllText(_configPath);

        // Sanity: the persisted JSON carries the v2 sub-object so a
        // hand inspection of the file shows the contract-shaped data,
        // not just the legacy flat fields.
        using (var doc = JsonDocument.Parse(saved))
        {
            var fileSink = doc.RootElement
                .GetProperty("sinks")
                .EnumerateArray()
                .Single(s => s.GetProperty("kind").GetString() == KnownSinkKinds.TextFile);
            fileSink.TryGetProperty("properties", out var bag).Should().BeTrue(
                "the persisted sink JSON must carry the v2 properties bag");
            bag.GetProperty("logFileTemplate").GetString().Should().Be("reboot-survivor");
            bag.GetProperty("rollingInterval").GetString().Should().Be("hourly");
            bag.GetProperty("maxRetainedFiles").GetInt32().Should().Be(24);
            bag.GetProperty("retentionDays").GetInt32().Should().Be(30);
        }

        // Reboot — a brand-new builder is what the host wires up at
        // startup before reading the saved JSON.
        var rebuiltBuilder = QuickLogBuilder.Create()
            .WithFileSinkProviders();
        HeraldManagementApi.RestoreBuilderFromConfig(rebuiltBuilder, saved);

        var inspection = rebuiltBuilder.Inspect();
        inspection.HasFileSink.Should().BeTrue("the file sink must come back after reboot");
        inspection.FilePath.Should().Be($"{TempDirAsForward}/reboot-survivor.log");
        inspection.FileMinLevel.Should().Be("info");
        inspection.HasFileRolling.Should().BeTrue();
        inspection.FileRollingInterval.Should().Be("hourly");
        inspection.FileMaxBytes.Should().Be(32L * 1024 * 1024);
        inspection.FileMaxRetainedFiles.Should().Be(24);
        inspection.FileNamePattern.Should().Be("_yyyyMMdd_HH");
        inspection.TotalSizeCapBytes.Should().Be(1L * 1024 * 1024 * 1024);
        inspection.RetentionDays.Should().Be(30);
    }

    [Fact]
    public void RestoreBuilderFromConfig_accepts_a_hand_written_v2_only_json()
    {
        // Operators sometimes hand-edit the pipeline JSON. v2-only
        // JSON has the properties bag but no legacy `path` / `rolling`
        // fields — RestoreBuilderFromConfig must rebuild the file
        // sink from the bag alone.
        var json = $$"""
            {
              "sinks": [
                {
                  "name": "text_file",
                  "kind": "text_file",
                  "minLevel": "debug",
                  "properties": {
                    "logDirectory":       "{{TempDirAsForward}}",
                    "logFileTemplate":    "hand-written",
                    "logExtension":       "log",
                    "logOutputType":      "text",
                    "rollingLogsEnabled": true,
                    "rollingInterval":    "daily",
                    "maxFileSize":        "4MB",
                    "maxRetainedFiles":   3,
                    "namePattern":        "",
                    "totalSizeCap":       "12MB",
                    "retentionDays":      7
                  }
                }
              ]
            }
            """;

        var rebuiltBuilder = QuickLogBuilder.Create()
            .WithFileSinkProviders();
        HeraldManagementApi.RestoreBuilderFromConfig(rebuiltBuilder, json);

        var inspection = rebuiltBuilder.Inspect();
        inspection.HasFileSink.Should().BeTrue();
        inspection.FilePath.Should().Be($"{TempDirAsForward}/hand-written.log");
        inspection.FileMinLevel.Should().Be("debug");
        inspection.HasFileRolling.Should().BeTrue();
        inspection.FileRollingInterval.Should().Be("daily");
        inspection.FileMaxBytes.Should().Be(4L * 1024 * 1024);
        inspection.FileMaxRetainedFiles.Should().Be(3);
        inspection.TotalSizeCapBytes.Should().Be(12L * 1024 * 1024);
        inspection.RetentionDays.Should().Be(7);
    }
}
