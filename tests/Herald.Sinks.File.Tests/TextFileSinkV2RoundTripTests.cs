// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
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
    public void Mixed_v1_and_v2_envelope_lets_v2_properties_win_through_reboot()
    {
        // Reproduces the dashboard payload an un-rebuilt UI sends:
        // BOTH the v2 `properties` sub-object AND the legacy flat
        // keys are present, with conflicting values. The v2 lift
        // must take the inner bag, ignore the stale flat keys, and
        // the saved JSON must carry only the NEW values forward so
        // a reboot restores them — not the older flat-key set.
        var (_, api) = CreatePipeline();
        var json = $$"""
            {
              "sinks": [
                {
                  "sinkId": "text_file",
                  "kind":   "text_file",
                  "alias":  null,
                  "config": {
                    "properties": {
                      "logDirectory":       "logsdddrrr",
                      "logExtension":       "csv",
                      "logFileTemplate":    "code-built-pipelined",
                      "logOutputType":      "text",
                      "maxFileSize":        "1MB",
                      "maxRetainedFiles":   32,
                      "namePattern":        "dd",
                      "retentionDays":      30,
                      "rollingInterval":    "hourly",
                      "rollingLogsEnabled": true,
                      "totalSizeCap":       "10MB"
                    },
                    "logDirectory":       "./logs",
                    "logFileTemplate":    "code-built-pipelined",
                    "logExtension":       "log",
                    "rollingLogsEnabled": true,
                    "rollingInterval":    "daily",
                    "maxFileSize":        "12Gb",
                    "maxRetainedFiles":   33,
                    "namePattern":        "MMdd",
                    "fileNamePattern":    "dd",
                    "totalSizeCap":       "10.1Mb",
                    "retentionDays":      302,
                    "logOutputType":      "text"
                  }
                }
              ]
            }
            """;

        api.CommitFull(json).Success.Should().BeTrue();

        // The persisted JSON must reflect the bag's values, not the
        // stale flat ones. If a reboot reads the flat keys it would
        // see logDirectory "./logs" / maxFileSize "12Gb" / etc., and
        // every operator edit would silently revert.
        System.IO.File.Exists(_configPath).Should().BeTrue();
        var saved = System.IO.File.ReadAllText(_configPath);

        var rebuiltBuilder = QuickLogBuilder.Create().WithFileSinkProviders();
        HeraldManagementApi.RestoreBuilderFromConfig(rebuiltBuilder, saved);

        var inspection = rebuiltBuilder.Inspect();
        inspection.HasFileSink.Should().BeTrue();
        inspection.FilePath.Should().Be("logsdddrrr/code-built-pipelined.csv",
            "the v2 bag's logDirectory + logFileTemplate + logExtension drive the path; the stale flat ./logs must lose.");
        inspection.HasFileRolling.Should().BeTrue();
        inspection.FileRollingInterval.Should().Be("hourly",
            "the bag picked hourly; the stale flat 'daily' must lose.");
        inspection.FileMaxBytes.Should().Be(1L * 1024 * 1024,
            "the bag picked 1MB; the stale flat 12Gb must lose.");
        inspection.FileMaxRetainedFiles.Should().Be(32);
        inspection.FileNamePattern.Should().Be("dd");
        inspection.RetentionDays.Should().Be(30);
        inspection.TotalSizeCapBytes.Should().Be(10L * 1024 * 1024);
    }

    [Fact]
    public void Browser_reopen_after_commit_sees_the_committed_values_via_GetPipelineFlow()
    {
        // The user-reported sequence: dashboard sends a v2-only
        // commit (no legacy flat keys, just `config: { properties }`),
        // gets a success response, then the operator closes the
        // browser. When a fresh browser opens it fetches the pipeline
        // flow and that response has to carry the just-committed
        // values — otherwise the form repaints with the boot-time
        // state and every edit looks like it reverted.
        //
        // The test runs CommitFull, then asks the SAME api instance
        // for GetPipelineFlow (the call the dashboard makes on
        // browser open) and asserts the file sink's properties bag
        // and flat-key snapshot both reflect the commit.
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
                      "logDirectory":       "logsdddrrr",
                      "logExtension":       "csv",
                      "logFileTemplate":    "code-built-pipelined",
                      "logOutputType":      "text",
                      "rollingLogsEnabled": true,
                      "rollingInterval":    "hourly",
                      "maxFileSize":        "1MB",
                      "maxRetainedFiles":   32,
                      "namePattern":        "dd",
                      "totalSizeCap":       "10MB",
                      "retentionDays":      30
                    }
                  }
                }
              ]
            }
            """;

        var commitResult = api.CommitFull(json);
        commitResult.Success.Should().BeTrue($"commit failed: {commitResult.Message}");

        // First "browser open" — same api instance. The dashboard's
        // pipeline editor reads from /api/registry/{name}/flow which
        // calls GetPipelineFlow under the hood.
        var flow = api.GetPipelineFlow();
        var fileSink = flow.Sinks.Single(s => s.SinkId == KnownSinkKinds.TextFile);

        fileSink.Config.Should().NotBeNull("the v2 publish path must surface a Config dictionary");
        var config = fileSink.Config!;

        config.Should().ContainKey("properties",
            "the dashboard reads `properties` to re-render the v2 form on every page reload");
        var bag = config["properties"] as IReadOnlyDictionary<string, object?>;
        bag.Should().NotBeNull("the properties value must be a dictionary, not opaque");
        bag!.Should().ContainKey("logDirectory");
        bag!["logDirectory"].Should().Be("logsdddrrr",
            "if this fails the form will repaint with the boot-time directory and the user sees a 'reverted' state.");
        bag!["logExtension"].Should().Be("csv");
        bag!["logFileTemplate"].Should().Be("code-built-pipelined");
        bag!["maxFileSize"].Should().Be("1MB");
        bag!["maxRetainedFiles"].Should().Be(32L);
        bag!["namePattern"].Should().Be("dd");
        bag!["rollingInterval"].Should().Be("hourly");
        bag!["totalSizeCap"].Should().Be("10MB");
        bag!["retentionDays"].Should().Be(30L);

        // The transitional flat keys must agree with the bag —
        // until every consumer migrates, ConfigPanelRight and other
        // legacy reader paths still read these.
        config["logDirectory"].Should().Be("logsdddrrr");
        config["logFileTemplate"].Should().Be("code-built-pipelined");
        config["logExtension"].Should().Be("csv");
        config["rollingLogsEnabled"].Should().Be(true);
        config["rollingInterval"].Should().Be("hourly");
        config["maxFileSize"].Should().Be("1MB");
        config["maxRetainedFiles"].Should().Be(32L);
        config["totalSizeCap"].Should().Be("10MB");
        config["retentionDays"].Should().Be(30L);

        // Second flow call — proves the response is stable across
        // requests (no per-call side effect that resets the builder).
        var flow2 = api.GetPipelineFlow();
        var fileSink2 = flow2.Sinks.Single(s => s.SinkId == KnownSinkKinds.TextFile);
        var bag2 = fileSink2.Config!["properties"] as IReadOnlyDictionary<string, object?>;
        bag2.Should().NotBeNull();
        bag2!["logDirectory"].Should().Be("logsdddrrr");
        bag2["maxFileSize"].Should().Be("1MB");
        bag2["maxRetainedFiles"].Should().Be(32L);
    }

    [Fact]
    public void Commit_to_one_pipeline_does_not_leak_into_a_sibling_pipeline()
    {
        // Server hosts two pipelines side by side
        // (code-built-pipeline and json-loaded-pipeline are the
        // canonical two in the default tenant). The dashboard's
        // commit endpoint takes the pipeline name in the URL —
        // /api/registry/{name}/commit — so each pipeline has its own
        // ConfigPath and its own builder. A commit to one must not
        // touch the other; if it does, an operator editing pipeline
        // A would see their changes "land" on pipeline B and pipeline
        // A's view would still show pre-edit values, looking exactly
        // like a revert.
        //
        // This pins server-side isolation. If the symptom reproduces
        // on the dashboard while this test passes, the routing bug
        // is on the client (wrong pipeline name in the POST URL).
        var tempA = Path.Combine(Path.GetTempPath(), $"herald_iso_a_{Guid.NewGuid():N}");
        var tempB = Path.Combine(Path.GetTempPath(), $"herald_iso_b_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempA);
        Directory.CreateDirectory(tempB);
        var configA = Path.Combine(tempA, "code-built-pipeline.json");
        var configB = Path.Combine(tempB, "json-loaded-pipeline.json");

        try
        {
            var builderA = QuickLogBuilder.Create("code-built-pipeline")
                .WithFileSinkProviders()
                .WithFileSink(Path.Combine(tempA, "a.log"))
                .WithMinimumLevel("info")
                .WithHotReload();
            var resultA = builderA.BuildAndCommit();
            var apiA = new HeraldManagementApi(builderA, resultA) { ConfigPath = configA };

            var builderB = QuickLogBuilder.Create("json-loaded-pipeline")
                .WithFileSinkProviders()
                .WithFileSink(Path.Combine(tempB, "b.log"))
                .WithMinimumLevel("info")
                .WithHotReload();
            var resultB = builderB.BuildAndCommit();
            var apiB = new HeraldManagementApi(builderB, resultB) { ConfigPath = configB };

            // Commit a v2 payload against pipeline A only.
            var json = $$"""
                {
                  "sinks": [
                    {
                      "sinkId": "text_file",
                      "kind":   "text_file",
                      "config": {
                        "properties": {
                          "logDirectory":       "{{tempA.Replace('\\', '/')}}",
                          "logFileTemplate":    "isolation-target",
                          "logExtension":       "csv",
                          "rollingLogsEnabled": true,
                          "rollingInterval":    "hourly",
                          "maxFileSize":        "1MB",
                          "maxRetainedFiles":   32,
                          "namePattern":        "dd",
                          "totalSizeCap":       "10MB",
                          "retentionDays":      30,
                          "logOutputType":      "text"
                        }
                      }
                    }
                  ]
                }
                """;

            apiA.CommitFull(json).Success.Should().BeTrue();

            // Pipeline A took the commit.
            var inspectionA = builderA.Inspect();
            inspectionA.FilePath.Should().EndWith("isolation-target.csv");
            inspectionA.FileRollingInterval.Should().Be("hourly");
            inspectionA.FileMaxBytes.Should().Be(1L * 1024 * 1024);

            // Pipeline B was NOT touched.
            var inspectionB = builderB.Inspect();
            inspectionB.FilePath.Should().EndWith("b.log",
                "a commit POSTed against the A endpoint must not mutate B's builder");
            inspectionB.HasFileRolling.Should().BeFalse();

            // Pipeline A's disk file was written; B's stays whatever
            // it was on creation (or absent — these tests don't seed
            // a B file, so the assertion is just that A's matches and
            // B's doesn't carry A's values).
            System.IO.File.Exists(configA).Should().BeTrue();
            var savedA = System.IO.File.ReadAllText(configA);
            savedA.Should().Contain("isolation-target");

            if (System.IO.File.Exists(configB))
            {
                var savedB = System.IO.File.ReadAllText(configB);
                savedB.Should().NotContain("isolation-target",
                    "pipeline B's saved JSON must not pick up pipeline A's commit values");
            }
        }
        finally
        {
            try { Directory.Delete(tempA, recursive: true); } catch { }
            try { Directory.Delete(tempB, recursive: true); } catch { }
        }
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
