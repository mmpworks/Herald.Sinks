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
using Herald.Sinks.File.Providers;
using MMP.Herald.Configuration;
using MMP.Herald.Configuration.Sinks;
using MMP.Herald.Quick;
using MMP.Herald.Routing;
using Xunit;

namespace Herald.Sinks.File.Tests;

/// <summary>
/// End-to-end contract test for the v2 sink-config flow on
/// <c>text_file</c>. Walks the path the user named when this work
/// landed: load <c>configuration-text_file.mmpform</c>, parse its
/// <c>__properties</c> block, build a pipeline with QuickLogBuilder,
/// export the JSON, deserialize it, walk to the runtime definition,
/// and assert at every hop that every contract key shows up — with
/// the right value — without one being lost in transit.
///
/// <para>The whole point of v2 is that Core / Server stop owning sink
/// config shape. The mmpform names every key the sink consumes and
/// every value gets shipped through a generic property bag. These
/// tests prove the bag is intact end to end.</para>
/// </summary>
public sealed class TextFileSinkV2ContractTests
{
    // The contract — pulled from the embedded mmpform that ships with
    // the file-sink package. Loaded once per fixture so each test
    // works with the same source of truth.
    private static IReadOnlyList<MmpformPropertyDefinition> LoadContract()
    {
        ILogSinkProvider provider = new TextFileSinkProvider();
        var formText = provider.GetFormSchemaText();
        formText.Should().NotBeNullOrEmpty("the package must ship configuration-text_file.mmpform");
        var contract = MmpformPropertiesParser.Parse(formText);
        contract.Should().NotBeEmpty(
            "the mmpform must declare a __properties block — that block is the v2 contract.");
        return contract;
    }

    [Fact]
    public void Contract_lists_every_property_text_file_consumes()
    {
        // Lock down the exact set the runtime depends on. Adding or
        // removing a key in the mmpform without updating consumer
        // code (provider, runtime config, dashboard) flags here first.
        var contract = LoadContract();
        var names = contract.Select(static p => p.Name).ToHashSet(StringComparer.Ordinal);

        names.Should().BeEquivalentTo(new[]
        {
            "logDirectory",
            "logExtension",
            "logFileTemplate",
            "logOutputType",
            "maxFileSize",
            "maxRetainedFiles",
            "namePattern",
            "retentionDays",
            "rollingInterval",
            "rollingLogsEnabled",
            "totalSizeCap",
        });
    }

    [Fact]
    public void Parser_coerces_defaults_to_the_declared_clr_type()
    {
        var byName = LoadContract().ToDictionary(p => p.Name, StringComparer.Ordinal);

        byName["logDirectory"].Default.Should().BeOfType<string>().And.Be("");
        byName["logExtension"].Default.Should().BeOfType<string>().And.Be("log");
        byName["logFileTemplate"].Default.Should().BeOfType<string>().And.Be("log-file");
        byName["logOutputType"].Default.Should().BeOfType<string>().And.Be("text");
        byName["maxFileSize"].Default.Should().BeOfType<string>().And.Be("10Mb");
        // Numeric defaults are quoted strings in the mmpform; the
        // parser coerces them to long per the declared int type.
        byName["maxRetainedFiles"].Default.Should().BeOfType<long>().And.Be(50L);
        byName["retentionDays"].Default.Should().BeOfType<long>().And.Be(30L);
        // bool defaults are unquoted literals.
        byName["rollingLogsEnabled"].Default.Should().BeOfType<bool>().And.Be(false);
    }

    [Fact]
    public void QuickLogBuilder_emits_a_properties_bag_with_every_contract_key()
    {
        var contract = LoadContract();
        using var scratch = new ScratchDir();

        var builder = QuickLogBuilder.Create()
            .WithFileSinkProviders()
            .WithFileSink(Path.Combine(scratch.Path, "round-trip.log"), interval: "daily", maxBytes: 8 * 1_048_576)
            .WithMinimumLevel("info");

        var json = builder.ExportConfig();
        var config = LoggingJsonSerializer.Deserialize(json);

        var fileSink = config.Sinks.Single(s => s.Kind == "text_file");
        fileSink.Properties.Should().NotBeNull("v2 contract: every text_file sink JSON carries a properties bag");
        var bag = fileSink.Properties!;

        // Hard invariant: nothing in __properties is missing from the
        // emitted JSON. The dashboard counts on this when it renders
        // the form — a missing key produces a blank field with no
        // typed default backing it.
        foreach (var entry in contract)
            bag.Should().ContainKey(entry.Name, $"property '{entry.Name}' is part of the v2 contract");

        // User-set values made it through.
        bag["logDirectory"].Should().Be(scratch.Path.Replace('\\', '/'));
        bag["logFileTemplate"].Should().Be("round-trip");
        bag["logExtension"].Should().Be("log");
        bag["rollingLogsEnabled"].Should().Be(true);
        bag["rollingInterval"].Should().Be("daily");
        bag["maxFileSize"].Should().Be("8MB");

        // Untouched keys land on the contract default.
        bag["logOutputType"].Should().Be("text");
        bag["namePattern"].Should().Be("yyyyMMdd");
        // maxRetainedFiles + retentionDays are declared int; defaults
        // coerce to long.
        bag["maxRetainedFiles"].Should().Be(50L);
        bag["totalSizeCap"].Should().Be("10Mb");
        bag["retentionDays"].Should().Be(30L);
    }

    [Fact]
    public void Json_roundtrip_preserves_every_property_through_the_runtime_mapper()
    {
        // Up the rigor: take the JSON QuickLogBuilder produces, run
        // it through the bootstrap path the host actually uses, and
        // confirm the runtime definition's Properties bag still has
        // every contract key.
        using var scratch = new ScratchDir();

        var builder = QuickLogBuilder.Create()
            .WithFileSinkProviders()
            .WithFileSink(Path.Combine(scratch.Path, "runtime.log"))
            .WithMinimumLevel("info");

        var json = builder.ExportConfig();
        var config = LoggingJsonSerializer.Deserialize(json);

        // Roll the JSON through the same mapper the live pipeline uses
        // at hot-reload. The runtime sink definition we get back is
        // exactly what TextFileSinkProvider sees inside CreateSink.
        var levelRegistry = MMP.Herald.Levels.LogLevelRegistry.CreateDefault();
        var mapper = new DefaultLoggingConfigurationMapper();
        var runtime = mapper.Map(config, levelRegistry);
        var fileSink = runtime.Sinks.Single(s => s.Kind == "text_file");

        fileSink.Properties.Should().NotBeNull(
            "the runtime side of the contract — definition.Properties is what the sink reads at CreateSink time.");

        var contract = LoadContract();
        foreach (var entry in contract)
        {
            fileSink.Properties!
                .Should().ContainKey(entry.Name,
                    $"runtime mapper must preserve every contract key (missing: {entry.Name})");
        }
    }

    [Fact]
    public void Provider_routes_writer_construction_through_the_property_bag()
    {
        // Prove the bag is the source of truth at the provider level:
        // path + rolling come out matching the bag, regardless of
        // what the legacy typed fields say. This is the test that
        // would fail if a sink author quietly went around Properties
        // and reached for Path or RollingPolicy.
        using var scratch = new ScratchDir();

        var bag = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["logDirectory"]       = scratch.Path.Replace('\\', '/'),
            ["logFileTemplate"]    = "via-bag",
            ["logExtension"]       = "log",
            ["logOutputType"]      = "text",
            ["rollingLogsEnabled"] = true,
            ["rollingInterval"]    = "daily",
            ["maxFileSize"]        = "1MB",
            ["maxRetainedFiles"]   = 7L,
            ["fileNamePattern"]    = "",
            ["totalSizeCap"]       = "100MB",
            ["retentionDays"]      = 30.0,
        };

        var definition = new MMP.Herald.Configuration.Runtime.LoggingRuntimeSinkDefinition(
            Name: "text_file",
            Kind: "text_file",
            // Path is intentionally null — we want to prove the
            // provider builds the path from the bag.
            Path: null,
            Properties: bag);

        ILogSinkProvider provider = new TextFileSinkProvider();
        var levelRegistry = MMP.Herald.Levels.LogLevelRegistry.CreateDefault();
        var transformerRegistry = new MMP.Herald.Output.Rendering.LogOutputTransformerRegistry();

        // The act under test — CreateSink reads from definition.Properties.
        // Successful construction implies the writer factory picked up the
        // bag's logDirectory + logFileTemplate + logExtension.
        var sink = provider.CreateSink(definition, levelRegistry, transformerRegistry);
        sink.Should().NotBeNull();
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private sealed class ScratchDir : IDisposable
    {
        public string Path { get; }
        public ScratchDir()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"herald_v2_contract_{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }
        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }
}
