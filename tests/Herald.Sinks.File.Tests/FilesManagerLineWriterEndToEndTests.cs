// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using FluentAssertions;
using Herald.Sinks.File.Providers;
using MMP.Herald.Configuration.Runtime;
using Xunit;

// Aliases to dodge the collision between the System.IO surface and
// the Herald.Sinks.File namespace this test file lives in.
using Sys = System.IO;

namespace Herald.Sinks.File.Tests;

/// <summary>
/// End-to-end behavioural specs for the v2 file-sink writer chain:
/// LoggingRuntimeSinkDefinition (with property bag) →
/// LogSinkFileWriterFactory → FilesManagerPolicyMapper →
/// FilesManagerLineWriter → MMP.RollingFiles.FilesManager.
///
/// <para>
/// The "no pre-existing files" guarantee: starting from an empty
/// temp directory, the first WriteLine after factory construction
/// creates the directory and the active file. The file lands at the
/// path the property bag described, with the lines the sink wrote,
/// in order.
/// </para>
///
/// <para>
/// The other half of the user contract — "the server does not mutate
/// the data" — is covered by the v2 round-trip tests that already
/// exist in this project. The mapper specs cover translation
/// faithfulness (every mmpform key lands on the right policy field).
/// These tests prove the chain runs end-to-end without a pre-existing
/// directory or file, which is the realistic startup case.
/// </para>
/// </summary>
public sealed class FilesManagerLineWriterEndToEndTests : IDisposable
{
    private readonly string _tempRoot;

    public FilesManagerLineWriterEndToEndTests()
    {
        // One temp root per test instance keeps the tests isolated.
        // GetRandomFileName guarantees uniqueness without colliding
        // with other test runs on the same machine.
        _tempRoot = Sys.Path.Combine(Sys.Path.GetTempPath(),
            "herald-sinks-file-e2e-" + Sys.Path.GetRandomFileName());
    }

    public void Dispose()
    {
        if (Sys.Directory.Exists(_tempRoot))
        {
            try { Sys.Directory.Delete(_tempRoot, recursive: true); }
            catch { /* best effort — leftover artefacts are not fatal */ }
        }
    }

    // ── Helpers ────────────────────────────────────────────────────

    private LoggingRuntimeSinkDefinition Definition(IReadOnlyDictionary<string, object?> bag) =>
        new(
            Name:       "test-file-sink",
            Kind:       "text_file",
            Properties: bag);

    private IReadOnlyDictionary<string, object?> SimpleBag() =>
        new Dictionary<string, object?>
        {
            ["logDirectory"]    = _tempRoot,
            ["logFileTemplate"] = "events",
            ["logExtension"]    = "log"
        };

    // The factory returns ILineWriter, but the v2 path always returns
    // FilesManagerLineWriter. Casting through the concrete type lets
    // the tests use `using` + Dispose and keeps the assertion that
    // the v2 bag actually routes through the new shim — if a future
    // change made the factory return something else for a v2 bag,
    // this cast would fail loudly rather than silently passing.
    private FilesManagerLineWriter Build(IReadOnlyDictionary<string, object?> bag) =>
        (FilesManagerLineWriter)LogSinkFileWriterFactory.Create(Definition(bag));

    // ── Lazy construction (the regression these tests guard) ──────

    [Fact]
    public void Construction_does_no_filesystem_io()
    {
        // The factory MUST NOT touch disk during construction. If it
        // did, a pipeline commit that builds writers but never writes
        // events would create empty directories on every save — the
        // legacy RollingFileLineWriter never did that, and tests
        // breaking on "ghost directories" is the regression this
        // contract prevents.
        Sys.Directory.Exists(_tempRoot).Should().BeFalse();

        using var writer = Build(SimpleBag());

        Sys.Directory.Exists(_tempRoot).Should().BeFalse(
            "factory + shim must defer filesystem IO until the first WriteLine");
    }

    // ── No pre-existing files: the realistic startup case ─────────

    [Fact]
    public void First_WriteLine_creates_directory_and_active_file_with_the_line()
    {
        Sys.Directory.Exists(_tempRoot).Should().BeFalse();

        // Write inside a using block so the file handle releases
        // before the read assertions; FilesManager holds the active
        // file with exclusive write access.
        using (var writer = Build(SimpleBag()))
        {
            writer.WriteLine("first line");
        }

        Sys.Directory.Exists(_tempRoot).Should().BeTrue(
            "the writer must create the policy's directory on first write");

        var files = Sys.Directory.GetFiles(_tempRoot);
        files.Should().HaveCount(1, "exactly one active file lands on first write");

        var activePath = files[0];
        Sys.Path.GetFileName(activePath).Should().StartWith("events");
        Sys.Path.GetExtension(activePath).Should().Be(".log");

        var content = Sys.File.ReadAllText(activePath);
        content.Should().Contain("first line");
    }

    [Fact]
    public void Multiple_WriteLines_append_to_the_same_active_file_in_order()
    {
        using (var writer = Build(SimpleBag()))
        {
            writer.WriteLine("alpha");
            writer.WriteLine("beta");
            writer.WriteLine("gamma");
            // Dispose flushes the underlying stream when the using block exits.
        }

        var files = Sys.Directory.GetFiles(_tempRoot);
        files.Should().HaveCount(1);
        var lines = Sys.File.ReadAllLines(files[0]);
        lines.Should().Equal("alpha", "beta", "gamma");
    }

    [Fact]
    public void Honours_extension_from_the_bag()
    {
        var bag = new Dictionary<string, object?>(SimpleBag()) { ["logExtension"] = "ndjson" };
        using (var writer = Build(bag))
        {
            writer.WriteLine("{\"k\":\"v\"}");
        }

        var files = Sys.Directory.GetFiles(_tempRoot);
        files.Should().HaveCount(1);
        Sys.Path.GetExtension(files[0]).Should().Be(".ndjson",
            "the bag's logExtension drives the on-disk extension");
    }

    [Fact]
    public void Honours_template_from_the_bag()
    {
        var bag = new Dictionary<string, object?>(SimpleBag()) { ["logFileTemplate"] = "service-audit" };
        using (var writer = Build(bag))
        {
            writer.WriteLine("audit entry");
        }

        var files = Sys.Directory.GetFiles(_tempRoot);
        Sys.Path.GetFileName(files[0]).Should().StartWith("service-audit",
            "the bag's logFileTemplate drives the file stem");
    }

    [Fact]
    public void Creates_nested_directories_that_did_not_exist()
    {
        // The bag specifies a deeper path than the temp root. The
        // chain must mkdir -p its way to the leaf — this is the
        // "first run on a fresh machine" startup case.
        var nested = Sys.Path.Combine(_tempRoot, "nested", "deeper", "still");
        var bag = new Dictionary<string, object?>(SimpleBag()) { ["logDirectory"] = nested };

        using var writer = Build(bag);
        writer.WriteLine("hello");

        Sys.Directory.Exists(nested).Should().BeTrue("the chain must create every missing directory level");
        Sys.Directory.GetFiles(nested).Should().HaveCount(1);
    }

    [Fact]
    public void Reuses_a_pre_existing_directory_without_clobbering_unrelated_files()
    {
        // The directory may already exist from a previous run of the
        // same sink. The chain must use it without disturbing
        // unrelated files that happen to live there.
        Sys.Directory.CreateDirectory(_tempRoot);
        var unrelated = Sys.Path.Combine(_tempRoot, "unrelated.txt");
        Sys.File.WriteAllText(unrelated, "do not touch");

        using (var writer = Build(SimpleBag()))
        {
            writer.WriteLine("a log line");
        }

        Sys.File.Exists(unrelated).Should().BeTrue();
        Sys.File.ReadAllText(unrelated).Should().Be("do not touch");

        // The new active file lands alongside, not replacing.
        Sys.Directory.GetFiles(_tempRoot).Should().HaveCount(2);
    }

    // ── Precedence: v2 bag wins over legacy fields ────────────────

    [Fact]
    public void Properties_bag_wins_over_legacy_Path_when_both_are_populated()
    {
        // The factory's v2 dispatch is `Properties is { Count: > 0 }`,
        // so a definition that carries BOTH the modern bag AND the
        // legacy typed Path should land on the v2 branch and ignore
        // Path. If a future change inverts this precedence, the bag
        // a user just edited would silently lose to a stale legacy
        // value. This test pins the contract.
        var def = new LoggingRuntimeSinkDefinition(
            Name:       "precedence-test",
            Kind:       "text_file",
            Path:       "/some/legacy/path.log", // should be ignored
            Properties: SimpleBag());

        // The factory must dispatch to the v2 path — i.e. return the
        // shim type, not a legacy writer. The cast itself is the
        // assertion; if the wrong branch fires, the cast throws.
        using var writer = (FilesManagerLineWriter)
            LogSinkFileWriterFactory.Create(def);

        writer.WriteLine("via the bag");
        writer.Dispose();

        // Files land at the bag's directory, never at "/some/legacy".
        Sys.Directory.Exists(_tempRoot).Should().BeTrue();
        Sys.Directory.Exists("/some/legacy").Should().BeFalse();
    }

    // ── Rolling triggers actually fire ────────────────────────────

    [Fact]
    public void Size_trigger_rolls_to_a_second_file_when_the_cap_is_exceeded()
    {
        // Tight size cap; many lines. The bag-→-policy-→-runtime
        // chain must produce more than one file when the cap trips.
        // If the rolling-toggle gating in the mapper drops the
        // maxFileSize value, this test catches it because exactly
        // one file lands instead of several.
        var bag = new Dictionary<string, object?>(SimpleBag())
        {
            ["rollingLogsEnabled"] = true,
            ["rollingInterval"]    = "daily",
            ["maxFileSize"]        = "200B"
        };

        using (var writer = Build(bag))
        {
            // Each line is well over 20 bytes including the newline,
            // so 20 lines guarantees several rolls past the 200-byte
            // cap.
            for (var i = 0; i < 20; i++)
            {
                writer.WriteLine("event-" + i + " — padding to push the file past the 200 byte cap");
            }
        }

        var files = Sys.Directory.GetFiles(_tempRoot);
        files.Length.Should().BeGreaterThan(1,
            "the size trigger from the bag must produce more than one file when the cap is exceeded");
    }

    // ── Disposal ──────────────────────────────────────────────────

    [Fact]
    public void Disposing_before_any_write_does_not_throw_or_create_files()
    {
        var writer = Build(SimpleBag());
        var act = () => writer.Dispose();
        act.Should().NotThrow();

        Sys.Directory.Exists(_tempRoot).Should().BeFalse(
            "disposing a writer that never wrote must not produce any side effects");
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var writer = Build(SimpleBag());
        writer.WriteLine("once");

        writer.Dispose();
        var act = () => writer.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void WriteLine_after_Dispose_throws_ObjectDisposedException()
    {
        var writer = Build(SimpleBag());
        writer.WriteLine("before");
        writer.Dispose();

        var act = () => writer.WriteLine("after");
        act.Should().Throw<ObjectDisposedException>();
    }
}
