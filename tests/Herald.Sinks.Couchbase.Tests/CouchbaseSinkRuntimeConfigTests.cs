// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using FluentAssertions;
using Herald.Sinks.Couchbase.Providers;
using MMP.Herald.Configuration.Runtime;
using Xunit;

namespace Herald.Sinks.Couchbase.Tests;

/// <summary>
/// Translation-faithfulness specs for
/// <see cref="CouchbaseSinkRuntimeConfig"/>. End-to-end CreateSink
/// tests stop at the missing-required-field guards because the
/// connection-string ctor opens a real Couchbase cluster — outside
/// the scope of a smoke test. Mqtt and the host-port CouchbaseLogSink
/// ctor share the same constraint.
/// </summary>
public sealed class CouchbaseSinkRuntimeConfigTests
{
    // ── Bag → Resolved ──────────────────────────────────────────────

    [Fact]
    public void Reads_all_six_keys_from_property_bag_when_present()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "couchbase",
            Kind: "couchbase",
            Properties: new Dictionary<string, object?>
            {
                ["connection_string"] = "couchbase://cluster.example.com",
                ["username"]          = "logger",
                ["password"]          = "secret",
                ["bucket"]            = "logs",
                ["scope"]             = "audit",
                ["collection"]        = "events"
            });

        var resolved = CouchbaseSinkRuntimeConfig.From(def);

        resolved.ConnectionString.Should().Be("couchbase://cluster.example.com");
        resolved.Username.Should().Be("logger");
        resolved.Password.Should().Be("secret");
        resolved.Bucket.Should().Be("logs");
        resolved.Scope.Should().Be("audit");
        resolved.Collection.Should().Be("events");
    }

    [Fact]
    public void Defaults_scope_and_collection_to_underscore_default_when_absent()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "couchbase",
            Kind: "couchbase",
            Properties: new Dictionary<string, object?>
            {
                ["connection_string"] = "couchbase://cluster.example.com",
                ["username"]          = "logger",
                ["password"]          = "secret",
                ["bucket"]            = "logs"
            });

        var resolved = CouchbaseSinkRuntimeConfig.From(def);

        resolved.Scope.Should().Be("_default");
        resolved.Collection.Should().Be("_default");
    }

    [Fact]
    public void Bag_wins_over_legacy_slots_when_both_are_set()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "couchbase",
            Kind: "couchbase",
            Uri:   "couchbase://legacy.example.com",
            Alias: "legacy-pass",
            Host:  "legacy-bucket",
            Properties: new Dictionary<string, object?>
            {
                ["connection_string"] = "couchbase://bag.example.com",
                ["username"]          = "bag-user",
                ["password"]          = "bag-pass",
                ["bucket"]            = "bag-bucket"
            });

        var resolved = CouchbaseSinkRuntimeConfig.From(def);

        resolved.ConnectionString.Should().Be("couchbase://bag.example.com");
        resolved.Password.Should().Be("bag-pass");
        resolved.Bucket.Should().Be("bag-bucket");
    }

    [Fact]
    public void Falls_back_to_legacy_slots_for_three_of_six_fields()
    {
        // Username, scope, and collection have no legacy mapping.
        var def = new LoggingRuntimeSinkDefinition(
            Name: "couchbase",
            Kind: "couchbase",
            Uri:   "couchbase://legacy.example.com",
            Alias: "legacy-pass",
            Host:  "legacy-bucket",
            Properties: new Dictionary<string, object?>
            {
                ["username"] = "bag-user"
            });

        var resolved = CouchbaseSinkRuntimeConfig.From(def);

        resolved.ConnectionString.Should().Be("couchbase://legacy.example.com");
        resolved.Password.Should().Be("legacy-pass");
        resolved.Bucket.Should().Be("legacy-bucket");
        resolved.Username.Should().Be("bag-user");
    }

    // ── Provider validation ─────────────────────────────────────────

    [Fact]
    public void Provider_throws_when_connection_string_is_missing()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "couchbase",
            Kind: "couchbase",
            Properties: new Dictionary<string, object?>
            {
                ["username"] = "u",
                ["password"] = "p",
                ["bucket"]   = "b"
            });

        var act = () => new CouchbaseLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*connection_string*");
    }

    [Fact]
    public void Provider_throws_when_username_is_missing()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "couchbase",
            Kind: "couchbase",
            Properties: new Dictionary<string, object?>
            {
                ["connection_string"] = "couchbase://x",
                ["password"]          = "p",
                ["bucket"]            = "b"
            });

        var act = () => new CouchbaseLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*username*");
    }

    [Fact]
    public void Provider_throws_when_password_is_missing()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "couchbase",
            Kind: "couchbase",
            Properties: new Dictionary<string, object?>
            {
                ["connection_string"] = "couchbase://x",
                ["username"]          = "u",
                ["bucket"]            = "b"
            });

        var act = () => new CouchbaseLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*password*");
    }

    [Fact]
    public void Provider_throws_when_bucket_is_missing()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name: "couchbase",
            Kind: "couchbase",
            Properties: new Dictionary<string, object?>
            {
                ["connection_string"] = "couchbase://x",
                ["username"]          = "u",
                ["password"]          = "p"
            });

        var act = () => new CouchbaseLogSinkProvider().CreateSink(def, null!, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*bucket*");
    }
}
