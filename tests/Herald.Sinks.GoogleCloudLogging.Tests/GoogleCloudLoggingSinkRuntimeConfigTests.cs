// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using FluentAssertions;
using Herald.Sinks.GoogleCloudLogging.Providers;
using MMP.Herald.Configuration.Runtime;
using Xunit;

namespace Herald.Sinks.GoogleCloudLogging.Tests;

/// <summary>
/// Translation-faithfulness specs for
/// <see cref="GoogleCloudLoggingSinkRuntimeConfig"/>. Tests stop at
/// the typed Resolved record and the provider's required-field
/// guards because the sink ctor calls
/// LoggingServiceV2Client.Create() — that path needs ADC, which
/// is integration-test territory.
/// </summary>
public sealed class GoogleCloudLoggingSinkRuntimeConfigTests
{
    // ── Bag → Resolved ──────────────────────────────────────────────

    [Fact]
    public void Reads_project_log_resource_type_and_labels_from_bag()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "gcp",
            Kind: "gcp_logging",
            Properties: new Dictionary<string, object?>
            {
                ["project_id"]      = "my-proj",
                ["log_id"]          = "audit",
                ["resource_type"]   = "gce_instance",
                ["resource_labels"] = "project_id=my-proj, zone=us-central1-a, instance_id=i-123"
            });

        var resolved = GoogleCloudLoggingSinkRuntimeConfig.From(def);

        resolved.ProjectId.Should().Be("my-proj");
        resolved.LogId.Should().Be("audit");
        resolved.Resource.Type.Should().Be("gce_instance");
        resolved.Resource.Labels["project_id"].Should().Be("my-proj");
        resolved.Resource.Labels["zone"].Should().Be("us-central1-a");
        resolved.Resource.Labels["instance_id"].Should().Be("i-123");
    }

    [Fact]
    public void Defaults_log_id_to_herald_and_resource_type_to_global()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "gcp",
            Kind: "gcp_logging",
            Properties: new Dictionary<string, object?>
            {
                ["project_id"] = "my-proj"
            });

        var resolved = GoogleCloudLoggingSinkRuntimeConfig.From(def);

        resolved.LogId.Should().Be("herald");
        resolved.Resource.Type.Should().Be("global");
        resolved.Resource.Labels.Should().BeEmpty();
    }

    [Fact]
    public void Bag_wins_over_legacy_slots_for_project_id_and_log_id()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "gcp",
            Kind: "gcp_logging",
            Uri:  "legacy-project",
            Host: "legacy-log",
            Properties: new Dictionary<string, object?>
            {
                ["project_id"] = "bag-project",
                ["log_id"]     = "bag-log"
            });

        var resolved = GoogleCloudLoggingSinkRuntimeConfig.From(def);
        resolved.ProjectId.Should().Be("bag-project");
        resolved.LogId.Should().Be("bag-log");
    }

    [Fact]
    public void Falls_back_to_legacy_slots_for_project_id_and_log_id()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "gcp",
            Kind: "gcp_logging",
            Uri:  "legacy-project",
            Host: "legacy-log");

        var resolved = GoogleCloudLoggingSinkRuntimeConfig.From(def);
        resolved.ProjectId.Should().Be("legacy-project");
        resolved.LogId.Should().Be("legacy-log");
    }

    [Fact]
    public void Tolerates_blank_pairs_and_typos_in_resource_labels()
    {
        // The form validates label syntax separately; the parser
        // silently drops stray commas and pairs without '=' so a
        // partial typo doesn't crash the pipeline boot.
        var def = new LoggingRuntimeSinkDefinition(
            Name: "gcp",
            Kind: "gcp_logging",
            Properties: new Dictionary<string, object?>
            {
                ["project_id"]      = "p",
                ["resource_labels"] = "zone=us-east-1, , malformed_pair_no_equals, =value_no_key, location=us-east-1"
            });

        var resolved = GoogleCloudLoggingSinkRuntimeConfig.From(def);

        resolved.Resource.Labels["zone"].Should().Be("us-east-1");
        resolved.Resource.Labels["location"].Should().Be("us-east-1");
        resolved.Resource.Labels.Should().HaveCount(2);
    }

    // ── Provider validation (no ADC) ────────────────────────────────

    [Fact]
    public void Provider_throws_when_project_id_is_missing()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "gcp",
            Kind: "gcp_logging");

        var act = () => new GoogleCloudLoggingSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*project_id*");
    }
}
