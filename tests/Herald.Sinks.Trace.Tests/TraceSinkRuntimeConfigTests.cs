// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System.Collections.Generic;
using FluentAssertions;
using Herald.Sinks.Trace.Providers;
using MMP.Herald.Configuration.Runtime;
using Xunit;

namespace Herald.Sinks.Trace.Tests;

/// <summary>
/// Translation-faithfulness specs for
/// <see cref="TraceSinkRuntimeConfig"/>. Confirms the property-bag
/// path (configuration-trace.mmpform) wins over the legacy
/// <c>Alias</c> slot, and that empty or missing values produce a
/// null category — the sink's "no prefix" mode.
/// </summary>
public sealed class TraceSinkRuntimeConfigTests
{
    [Fact]
    public void Reads_category_from_property_bag_when_present()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "trace",
            Kind: "trace",
            Properties: new Dictionary<string, object?>
            {
                ["category"] = "Herald.Tests"
            });

        TraceSinkRuntimeConfig.ResolveCategory(def).Should().Be("Herald.Tests");
    }

    [Fact]
    public void Bag_wins_over_legacy_alias_when_both_are_set()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "trace",
            Kind: "trace",
            Alias: "Legacy.Category",
            Properties: new Dictionary<string, object?>
            {
                ["category"] = "Bag.Category"
            });

        TraceSinkRuntimeConfig.ResolveCategory(def).Should().Be("Bag.Category");
    }

    [Fact]
    public void Falls_back_to_alias_when_bag_is_null()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "trace",
            Kind: "trace",
            Alias: "Legacy.Category");

        TraceSinkRuntimeConfig.ResolveCategory(def).Should().Be("Legacy.Category");
    }

    [Fact]
    public void Falls_back_to_alias_when_bag_has_empty_category()
    {
        // An empty-string bag entry mirrors a form field the operator
        // left blank — that should not stomp the legacy alias slot.
        var def = new LoggingRuntimeSinkDefinition(
            Name: "trace",
            Kind: "trace",
            Alias: "Legacy.Category",
            Properties: new Dictionary<string, object?>
            {
                ["category"] = ""
            });

        TraceSinkRuntimeConfig.ResolveCategory(def).Should().Be("Legacy.Category");
    }

    [Fact]
    public void Returns_null_when_neither_bag_nor_alias_is_set()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "trace",
            Kind: "trace");

        TraceSinkRuntimeConfig.ResolveCategory(def).Should().BeNull();
    }

    [Fact]
    public void Treats_whitespace_only_alias_as_unset()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "trace",
            Kind: "trace",
            Alias: "   ");

        TraceSinkRuntimeConfig.ResolveCategory(def).Should().BeNull();
    }
}
