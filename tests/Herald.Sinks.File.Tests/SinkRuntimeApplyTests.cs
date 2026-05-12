// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Herald.Sinks.File;
using MMP.Herald.Addons.ManagementApi;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Quick;
using MMP.Herald.Routing.Loopback;
using MMP.Herald.Services;
using Xunit;

namespace Herald.Sinks.File.Tests;

/// <summary>
/// Pins the per-sink runtime strip's commit-on-immediate-change
/// contract end to end. The dashboard's strip flips one of four
/// fields — <c>runState</c>, <c>minLevel</c>, <c>teeLiveToFile</c>,
/// <c>teeLiveToUrl</c> — and the operator expects three things to
/// happen in lockstep:
///
///   1. The in-memory holder updates so the next event respects the
///      new value (no pipeline rebuild).
///   2. The builder's <see cref="SinkRuntimeOverrideSet"/> records
///      the new snapshot.
///   3. <c>PersistConfig()</c> writes the JSON file so a reboot
///      restores the operator's choice.
///
/// Each test below sends one PATCH-equivalent through
/// <see cref="HeraldManagementApi.ApplySinkRuntime"/> — the single
/// funnel both server endpoints route through — and asserts all
/// three properties hold. Coverage spans every field individually
/// plus a multi-field combined PATCH so no logic path silently
/// skips persistence.
/// </summary>
public sealed class SinkRuntimeApplyTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;
    private readonly QuickLogBuilder _builder;
    private readonly HeraldManagementApi _api;
    private readonly QuickLogResult _result;
    private const string PipelineName = "rt-apply-tests";
    private const string SinkId = "text_file";

    public SinkRuntimeApplyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"herald_rt_apply_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "pipeline.json");

        // Hot-reload is intentionally OFF for these tests. The
        // ApplySinkRuntime funnel writes the JSON file on every
        // call; with hot-reload on, the file watcher races with the
        // next PATCH and would build a fresh holder from a stale
        // JSON snapshot. We test the persist path here; the
        // reload-after-persist behaviour is covered separately.
        _builder = QuickLogBuilder.Create(PipelineName)
            .WithFileSinkProviders()
            .WithFileSink(Path.Combine(_tempDir, "initial.log"))
            .WithMinimumLevel("info");
        _result = _builder.BuildAndCommit();
        HeraldRegistry.Register(PipelineName, _builder, _result, _configPath);
        _api = new HeraldManagementApi(_builder, _result) { ConfigPath = _configPath };
    }

    public void Dispose()
    {
        try { HeraldRegistry.Remove(PipelineName); } catch { }
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    // ── runState ─────────────────────────────────────────────────────

    [Fact]
    public void ApplySinkRuntime_runState_disabled_lands_in_holder_builder_and_disk()
    {
        var apply = _api.ApplySinkRuntime(PipelineName, SinkId,
            new SinkRuntimeOverride(RunState: "disabled"));

        apply.Result.Success.Should().BeTrue($"apply failed: {apply.Result.Message}");
        apply.RunState.Should().Be("disabled");

        // Hop 1: holder reflects the change for next-event behaviour.
        SinkRunStateRegistry.Get(PipelineName, SinkId)!
            .Current.Should().Be(SinkRunState.Disabled);

        // Hop 2: builder mirror so the JSON serializer picks it up.
        _builder.SinkRuntimeOverrides.Get(SinkId)!
            .RunState.Should().Be("disabled");

        // Hop 3: on-disk JSON carries it forward.
        var saved = System.IO.File.ReadAllText(_configPath);
        saved.Should().Contain("\"runState\": \"disabled\"",
            "the strip's choice has to survive a reboot — that's what 'commit' means here.");
    }

    [Fact]
    public void ApplySinkRuntime_runState_test_lands_in_holder_builder_and_disk()
    {
        var apply = _api.ApplySinkRuntime(PipelineName, SinkId,
            new SinkRuntimeOverride(RunState: "test"));

        apply.Result.Success.Should().BeTrue();
        SinkRunStateRegistry.Get(PipelineName, SinkId)!
            .Current.Should().Be(SinkRunState.Test);
        _builder.SinkRuntimeOverrides.Get(SinkId)!
            .RunState.Should().Be("test");
        System.IO.File.ReadAllText(_configPath).Should().Contain("\"runState\": \"test\"");
    }

    [Fact]
    public void ApplySinkRuntime_rejects_unknown_runState_without_touching_state()
    {
        var apply = _api.ApplySinkRuntime(PipelineName, SinkId,
            new SinkRuntimeOverride(RunState: "garbage"));

        apply.Result.Success.Should().BeFalse();
        apply.Result.Message.Should().Contain("Unknown runState");

        // No partial application — holder unchanged, no override
        // recorded on the builder. A bad PATCH must be a no-op.
        SinkRunStateRegistry.Get(PipelineName, SinkId)!
            .Current.Should().Be(SinkRunState.Live);
        _builder.SinkRuntimeOverrides.Get(SinkId).Should().BeNull();
    }

    // ── teeLiveToFile / teeLiveToUrl ─────────────────────────────────

    [Fact]
    public void ApplySinkRuntime_teeLiveToFile_lands_in_holder_builder_and_disk()
    {
        var apply = _api.ApplySinkRuntime(PipelineName, SinkId,
            new SinkRuntimeOverride(TeeLiveToFile: true));

        apply.Result.Success.Should().BeTrue();
        apply.TeeLiveToFile.Should().Be(true);

        SinkOverridesRegistry.Get(PipelineName, SinkId)!.TeeLiveToFile.Should().BeTrue();
        _builder.SinkRuntimeOverrides.Get(SinkId)!.TeeLiveToFile.Should().BeTrue();
        System.IO.File.ReadAllText(_configPath).Should().Contain("\"teeLiveToFile\": true");
    }

    [Fact]
    public void ApplySinkRuntime_teeLiveToUrl_lands_in_holder_builder_and_disk()
    {
        var apply = _api.ApplySinkRuntime(PipelineName, SinkId,
            new SinkRuntimeOverride(TeeLiveToUrl: true));

        apply.Result.Success.Should().BeTrue();
        apply.TeeLiveToUrl.Should().Be(true);

        SinkOverridesRegistry.Get(PipelineName, SinkId)!.TeeLiveToUrl.Should().BeTrue();
        _builder.SinkRuntimeOverrides.Get(SinkId)!.TeeLiveToUrl.Should().BeTrue();
        System.IO.File.ReadAllText(_configPath).Should().Contain("\"teeLiveToUrl\": true");
    }

    // ── per-sink minLevel ────────────────────────────────────────────

    [Fact]
    public void ApplySinkRuntime_minLevel_lands_in_holder_builder_and_disk()
    {
        var apply = _api.ApplySinkRuntime(PipelineName, SinkId,
            new SinkRuntimeOverride(MinLevel: "warn"));

        apply.Result.Success.Should().BeTrue();
        apply.MinLevel.Should().Be("warn");

        SinkOverridesRegistry.Get(PipelineName, SinkId)!.MinLevel!.Key.Should().Be("warn");
        _builder.SinkRuntimeOverrides.Get(SinkId)!.MinLevel.Should().Be("warn");
        System.IO.File.ReadAllText(_configPath).Should().Contain("\"minLevel\": \"warn\"");
    }

    [Fact]
    public void ApplySinkRuntime_minLevel_none_clears_the_gate()
    {
        // Set warn first, then clear with "none" — proves the clear
        // path lands the same way the set path does.
        _api.ApplySinkRuntime(PipelineName, SinkId, new SinkRuntimeOverride(MinLevel: "warn"));
        var apply = _api.ApplySinkRuntime(PipelineName, SinkId, new SinkRuntimeOverride(MinLevel: "none"));

        apply.Result.Success.Should().BeTrue();
        apply.MinLevel.Should().Be("none");
        SinkOverridesRegistry.Get(PipelineName, SinkId)!.MinLevel.Should().BeNull();
    }

    [Fact]
    public void ApplySinkRuntime_rejects_unknown_minLevel_without_touching_state()
    {
        var apply = _api.ApplySinkRuntime(PipelineName, SinkId,
            new SinkRuntimeOverride(MinLevel: "not-a-level"));

        apply.Result.Success.Should().BeFalse();
        apply.Result.Message.Should().Contain("Unknown level");
        SinkOverridesRegistry.Get(PipelineName, SinkId)!.MinLevel.Should().BeNull();
    }

    // ── multi-field PATCH ────────────────────────────────────────────

    [Fact]
    public void ApplySinkRuntime_multi_field_PATCH_persists_every_field()
    {
        // Mirrors the rare case where the dashboard sends more than
        // one field in a single click (e.g. a future "preset" button).
        // The funnel must apply all four atomically and persist once.
        var apply = _api.ApplySinkRuntime(PipelineName, SinkId, new SinkRuntimeOverride(
            RunState:      "test",
            MinLevel:      "error",
            TeeLiveToFile: true,
            TeeLiveToUrl:  true));

        apply.Result.Success.Should().BeTrue();
        apply.RunState.Should().Be("test");
        apply.MinLevel.Should().Be("error");
        apply.TeeLiveToFile.Should().Be(true);
        apply.TeeLiveToUrl.Should().Be(true);

        var saved = System.IO.File.ReadAllText(_configPath);
        saved.Should().Contain("\"runState\": \"test\"");
        saved.Should().Contain("\"minLevel\": \"error\"");
        saved.Should().Contain("\"teeLiveToFile\": true");
        saved.Should().Contain("\"teeLiveToUrl\": true");
    }

    // ── single-field PATCH leaves siblings alone ─────────────────────

    [Fact]
    public void ApplySinkRuntime_single_field_PATCH_does_not_overwrite_other_fields()
    {
        // Set everything to a known state, then PATCH only one field.
        // The other three must keep their previous values — that's
        // what "merge non-null fields" means for the strip's UX.
        _api.ApplySinkRuntime(PipelineName, SinkId, new SinkRuntimeOverride(
            RunState:      "test",
            MinLevel:      "warn",
            TeeLiveToFile: true,
            TeeLiveToUrl:  true));

        // Now flip only runState.
        _api.ApplySinkRuntime(PipelineName, SinkId, new SinkRuntimeOverride(RunState: "live"));

        var snapshot = _builder.SinkRuntimeOverrides.Get(SinkId)!;
        snapshot.RunState.Should().Be("live");
        snapshot.MinLevel.Should().Be("warn",
            "a single-field PATCH must not silently clear sibling fields");
        snapshot.TeeLiveToFile.Should().Be(true);
        snapshot.TeeLiveToUrl.Should().Be(true);

        var holder = SinkOverridesRegistry.Get(PipelineName, SinkId)!;
        holder.MinLevel!.Key.Should().Be("warn");
        holder.TeeLiveToFile.Should().BeTrue();
        holder.TeeLiveToUrl.Should().BeTrue();
    }

    // ── reboot proves persistence ────────────────────────────────────

    [Fact]
    public void Saved_runtime_state_restores_through_RestoreBuilderFromConfig()
    {
        // The reboot scenario: apply the strip's choice, the JSON
        // file holds it, and a fresh builder rebuilt from that JSON
        // sees the same state through SinkRuntimeOverrides. This
        // pins the boot-side half of "commit" — the disk-write side
        // is covered in the field-by-field tests above.
        _api.ApplySinkRuntime(PipelineName, SinkId, new SinkRuntimeOverride(
            RunState:      "disabled",
            MinLevel:      "error",
            TeeLiveToFile: true,
            TeeLiveToUrl:  true));

        var saved = System.IO.File.ReadAllText(_configPath);

        var rebuilt = QuickLogBuilder.Create(PipelineName).WithFileSinkProviders();
        HeraldManagementApi.RestoreBuilderFromConfig(rebuilt, saved);

        var snapshot = rebuilt.SinkRuntimeOverrides.Get(SinkId);
        snapshot.Should().NotBeNull("the saved JSON must rehydrate the runtime override map");
        snapshot!.RunState.Should().Be("disabled");
        snapshot.MinLevel.Should().Be("error");
        snapshot.TeeLiveToFile.Should().BeTrue();
        snapshot.TeeLiveToUrl.Should().BeTrue();
    }

    [Fact]
    public void ApplySinkRuntime_rejects_unknown_sink_with_failure_result()
    {
        // The single funnel must surface "unknown sink" as a clean
        // failure — never a half-applied state where some field
        // landed before validation noticed the sink id was wrong.
        var apply = _api.ApplySinkRuntime(PipelineName, "ghost_sink",
            new SinkRuntimeOverride(RunState: "disabled"));

        apply.Result.Success.Should().BeFalse();
        apply.Result.Message.Should().Contain("ghost_sink");
        _builder.SinkRuntimeOverrides.Get("ghost_sink").Should().BeNull();
    }

    // ── PERFORMANCE — the strip flip must be near-instant ───────────
    //
    // History: an early version of the persist path called
    // QuickLogBuilder.ExportConfigToFile, which calls Build(), which
    // bootstraps the entire runtime — every sink, every router, every
    // level registry. A single dashboard click took 11 seconds end to
    // end. The fix routes PersistConfig through ExportConfigJsonToFile
    // (no Bootstrap, no rebuild). These tests make sure that path
    // stays the path.

    [Fact]
    public void ApplySinkRuntime_does_not_rebuild_the_running_pipeline()
    {
        // Identity check: if PersistConfig (or anything in the funnel)
        // triggers a rebuild, the live pipeline kernel reference
        // changes. We capture it before the PATCH and confirm it is
        // the same reference after — proves no rebuild fired during
        // the runtime PATCH.
        var kernelBefore = _result.Logger;
        _api.ApplySinkRuntime(PipelineName, SinkId,
            new SinkRuntimeOverride(RunState: "disabled"));
        var kernelAfter = _result.Logger;

        kernelAfter.Should().BeSameAs(kernelBefore,
            "the runtime PATCH path must not rebuild the live pipeline; "
            + "rebuild is the caller's job (CommitFull / RebuildWithDowntime).");
    }

    [Fact]
    public void ApplySinkRuntime_completes_inside_a_tight_budget()
    {
        // Tight budget per call. A single Build() bootstrap on a
        // realistic pipeline can take seconds; the lightweight
        // serialize path is microseconds. 250 ms is two orders of
        // magnitude above the realistic cost and still fails fast
        // if the heavyweight path leaks back in. Paired with the
        // 100x burst test below, this catches both single-call and
        // accumulated regressions.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var apply = _api.ApplySinkRuntime(PipelineName, SinkId,
            new SinkRuntimeOverride(RunState: "test"));
        sw.Stop();

        apply.Result.Success.Should().BeTrue();
        sw.ElapsedMilliseconds.Should().BeLessThan(250,
            "a strip click must feel instant; if this fails the persist path is rebuilding the pipeline.");
    }

    [Fact]
    public void ApplySinkRuntime_burst_of_100_PATCHES_finishes_under_one_second()
    {
        // 100 rapid PATCHes — the dashboard's strip can't actually
        // fire this fast, but the test catches "death by a thousand
        // cuts" regressions where each call adds, say, 50ms of
        // bootstrap cost. With Build() in the path this loop would
        // take ~1100s; with the lightweight path it's well under a
        // second.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 100; i++)
        {
            var next = (i % 2 == 0) ? "disabled" : "live";
            _api.ApplySinkRuntime(PipelineName, SinkId,
                new SinkRuntimeOverride(RunState: next));
        }
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(1000,
            "100 PATCHes through the runtime funnel must stay inside one second; "
            + "if this fails the persist path is rebuilding the pipeline per call.");
    }

    [Fact]
    public void PersistConfig_path_writes_JSON_without_invoking_the_bootstrap()
    {
        // Direct test of the lightweight serialize path on the
        // builder. ExportConfigJson should produce the same JSON
        // shape ExportConfig does, but without bootstrapping the
        // runtime — which is what makes it cheap enough to call on
        // every per-sink runtime click.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var json = _builder.ExportConfigJson();
        sw.Stop();

        json.Should().NotBeNullOrWhiteSpace();
        json.Should().Contain("\"sinks\":");
        json.Should().Contain("\"pipelineSteps\":");
        sw.ElapsedMilliseconds.Should().BeLessThan(100,
            "ExportConfigJson must skip Build() and stay near-instant.");
    }
}
