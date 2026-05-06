// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System.Collections.Generic;
using FluentAssertions;
using Herald.Sinks.File.Providers;
using MMP.Herald.Configuration.Runtime;
using MMP.RollingFiles;
using Xunit;

namespace Herald.Sinks.File.Tests;

/// <summary>
/// Translation-faithfulness specs for
/// <see cref="FilesManagerPolicyMapper"/>. Each test starts with a
/// representative property bag (mirroring what the dashboard PATCH +
/// QuickLogBuilder paths produce) and asserts the translated
/// <see cref="FilesManagerPolicy"/> carries every value at its right
/// field. The mapper is the single seam where the bag becomes
/// runtime — if a key drifts here, the whole pipeline drifts.
/// </summary>
public sealed class FilesManagerPolicyMapperTests
{
    // ── Helpers ────────────────────────────────────────────────────

    private static LoggingRuntimeSinkDefinition Definition(IReadOnlyDictionary<string, object?> bag) =>
        new(
            Name:       "test-file-sink",
            Kind:       "text_file",
            Properties: bag);

    private static IReadOnlyDictionary<string, object?> RequiredBag() =>
        new Dictionary<string, object?>
        {
            ["logDirectory"]    = "/var/log/herald",
            ["logFileTemplate"] = "audit"
        };

    // ── Required keys ──────────────────────────────────────────────

    [Fact]
    public void Translates_directory_and_template_into_the_policy()
    {
        var bag = new Dictionary<string, object?>(RequiredBag());
        var policy = FilesManagerPolicyMapper.From(Definition(bag));
        policy.Directory.Should().Be("/var/log/herald");
        policy.FileNameTemplate.Should().Be("audit");
    }

    [Fact]
    public void Throws_when_directory_is_missing()
    {
        var bag = new Dictionary<string, object?> { ["logFileTemplate"] = "x" };
        var act = () => FilesManagerPolicyMapper.From(Definition(bag));
        act.Should().Throw<System.InvalidOperationException>().WithMessage("*logDirectory*");
    }

    [Fact]
    public void Throws_when_template_is_missing()
    {
        var bag = new Dictionary<string, object?> { ["logDirectory"] = "/x" };
        var act = () => FilesManagerPolicyMapper.From(Definition(bag));
        act.Should().Throw<System.InvalidOperationException>().WithMessage("*logFileTemplate*");
    }

    [Fact]
    public void Throws_when_properties_bag_is_null()
    {
        var def = new LoggingRuntimeSinkDefinition(
            Name:       "no-bag",
            Kind:       "text_file",
            Properties: null);
        var act = () => FilesManagerPolicyMapper.From(def);
        act.Should().Throw<System.InvalidOperationException>().WithMessage("*Properties bag*");
    }

    // ── Extension normalisation ────────────────────────────────────

    [Fact]
    public void Adds_leading_dot_to_extension_when_missing()
    {
        var bag = new Dictionary<string, object?>(RequiredBag()) { ["logExtension"] = "ndjson" };
        var policy = FilesManagerPolicyMapper.From(Definition(bag));
        policy.Extension.Should().Be(".ndjson");
    }

    [Fact]
    public void Preserves_extension_with_leading_dot()
    {
        var bag = new Dictionary<string, object?>(RequiredBag()) { ["logExtension"] = ".log" };
        var policy = FilesManagerPolicyMapper.From(Definition(bag));
        policy.Extension.Should().Be(".log");
    }

    [Fact]
    public void Defaults_extension_to_dot_log_when_unset()
    {
        var policy = FilesManagerPolicyMapper.From(Definition(RequiredBag()));
        policy.Extension.Should().Be(".log");
    }

    // ── Rolling-trigger gating ────────────────────────────────────

    [Fact]
    public void Leaves_rolling_off_when_rollingLogsEnabled_is_false()
    {
        var bag = new Dictionary<string, object?>(RequiredBag())
        {
            ["rollingLogsEnabled"] = false,
            ["rollingInterval"]    = "daily",
            ["maxFileSize"]        = "10MB"
        };
        var policy = FilesManagerPolicyMapper.From(Definition(bag));
        policy.Interval.Should().Be(RollingInterval.None);
        policy.MaxBytesPerFile.Should().BeNull();
    }

    [Fact]
    public void Promotes_rolling_when_rollingLogsEnabled_is_true()
    {
        var bag = new Dictionary<string, object?>(RequiredBag())
        {
            ["rollingLogsEnabled"] = true,
            ["rollingInterval"]    = "daily",
            ["maxFileSize"]        = "10MB"
        };
        var policy = FilesManagerPolicyMapper.From(Definition(bag));
        policy.Interval.Should().Be(RollingInterval.Daily);
        policy.MaxBytesPerFile.Should().Be(10L * 1024 * 1024);
    }

    [Theory]
    [InlineData("hourly", RollingInterval.Hourly)]
    [InlineData("Hourly", RollingInterval.Hourly)]
    [InlineData("daily",  RollingInterval.Daily)]
    [InlineData("custom", RollingInterval.Custom)]
    [InlineData("",       RollingInterval.None)]
    [InlineData("bogus",  RollingInterval.None)]
    public void Parses_interval_case_insensitively(string raw, RollingInterval expected)
    {
        var bag = new Dictionary<string, object?>(RequiredBag())
        {
            ["rollingLogsEnabled"] = true,
            ["rollingInterval"]    = raw
        };
        var policy = FilesManagerPolicyMapper.From(Definition(bag));
        policy.Interval.Should().Be(expected);
    }

    // ── Human byte sizes ──────────────────────────────────────────

    [Theory]
    [InlineData("10MB", 10L * 1024 * 1024)]
    [InlineData("1GB",  1L * 1024 * 1024 * 1024)]
    [InlineData("500KB", 500L * 1024)]
    [InlineData("0",     null)]
    [InlineData("",      null)]
    [InlineData("garbage", null)]
    public void Parses_max_file_size_human_strings(string raw, long? expected)
    {
        var bag = new Dictionary<string, object?>(RequiredBag())
        {
            ["rollingLogsEnabled"] = true,
            ["maxFileSize"]        = raw
        };
        var policy = FilesManagerPolicyMapper.From(Definition(bag));
        policy.MaxBytesPerFile.Should().Be(expected);
    }

    [Fact]
    public void Parses_total_size_cap_independently_of_rolling_toggle()
    {
        // totalSizeCap is a retention bound, not a roll trigger — it
        // applies even when rollingLogsEnabled is false (you can still
        // ask for a max-disk-usage cap on the single file's pile).
        var bag = new Dictionary<string, object?>(RequiredBag())
        {
            ["totalSizeCap"] = "10MB"
        };
        var policy = FilesManagerPolicyMapper.From(Definition(bag));
        policy.TotalSizeCapBytes.Should().Be(10L * 1024 * 1024);
    }

    // ── Retention bounds ──────────────────────────────────────────

    [Fact]
    public void Carries_max_retained_files_and_retention_days()
    {
        var bag = new Dictionary<string, object?>(RequiredBag())
        {
            ["maxRetainedFiles"] = 30,
            ["retentionDays"]    = 14
        };
        var policy = FilesManagerPolicyMapper.From(Definition(bag));
        policy.MaxRetainedFiles.Should().Be(30);
        policy.RetentionDays.Should().Be(14);
    }

    [Fact]
    public void Accepts_string_numerics_from_json_deserialiser()
    {
        // The dashboard's JSON sometimes emits numbers as quoted strings
        // (mmpform stores defaults as quoted text). The mapper must
        // accept both shapes — otherwise the bag's value silently
        // becomes null and the pipeline reverts to defaults.
        var bag = new Dictionary<string, object?>(RequiredBag())
        {
            ["maxRetainedFiles"] = "30",
            ["retentionDays"]    = "14"
        };
        var policy = FilesManagerPolicyMapper.From(Definition(bag));
        policy.MaxRetainedFiles.Should().Be(30);
        policy.RetentionDays.Should().Be(14);
    }

    // ── Name pattern (legacy + current spelling) ──────────────────

    [Fact]
    public void Reads_name_pattern_via_namePattern_first()
    {
        var bag = new Dictionary<string, object?>(RequiredBag())
        {
            ["namePattern"]     = "yyyyMMdd",
            ["fileNamePattern"] = "should-lose"
        };
        var policy = FilesManagerPolicyMapper.From(Definition(bag));
        policy.FileNameSuffix.Should().Be("yyyyMMdd");
    }

    [Fact]
    public void Falls_back_to_legacy_fileNamePattern_when_namePattern_missing()
    {
        var bag = new Dictionary<string, object?>(RequiredBag())
        {
            ["fileNamePattern"] = "yyyyMMdd"
        };
        var policy = FilesManagerPolicyMapper.From(Definition(bag));
        policy.FileNameSuffix.Should().Be("yyyyMMdd");
    }

    [Fact]
    public void Treats_empty_name_pattern_as_unset()
    {
        var bag = new Dictionary<string, object?>(RequiredBag())
        {
            ["namePattern"] = ""
        };
        var policy = FilesManagerPolicyMapper.From(Definition(bag));
        policy.FileNameSuffix.Should().BeNull();
    }

    // ── New mmpform-extension keys (default off) ──────────────────

    [Fact]
    public void Defaults_compression_to_none_when_unset()
    {
        var policy = FilesManagerPolicyMapper.From(Definition(RequiredBag()));
        policy.Compression.Should().Be(CompressionMode.None);
    }

    [Theory]
    [InlineData("gzip",   CompressionMode.Gzip)]
    [InlineData("Gzip",   CompressionMode.Gzip)]
    [InlineData("brotli", CompressionMode.Brotli)]
    [InlineData("None",   CompressionMode.None)]
    [InlineData("",       CompressionMode.None)]
    public void Parses_compression_when_present(string raw, CompressionMode expected)
    {
        var bag = new Dictionary<string, object?>(RequiredBag()) { ["compression"] = raw };
        var policy = FilesManagerPolicyMapper.From(Definition(bag));
        policy.Compression.Should().Be(expected);
    }

    [Fact]
    public void Defaults_disk_full_to_stop_when_unset()
    {
        var policy = FilesManagerPolicyMapper.From(Definition(RequiredBag()));
        policy.DiskFull.Should().Be(DiskFullStrategy.Stop);
    }

    [Theory]
    [InlineData("Drop",     DiskFullStrategy.Drop)]
    [InlineData("fallback", DiskFullStrategy.Fallback)]
    [InlineData("Stop",     DiskFullStrategy.Stop)]
    public void Parses_disk_full_when_present(string raw, DiskFullStrategy expected)
    {
        var bag = new Dictionary<string, object?>(RequiredBag()) { ["diskFull"] = raw };
        var policy = FilesManagerPolicyMapper.From(Definition(bag));
        policy.DiskFull.Should().Be(expected);
    }

    [Fact]
    public void Defaults_clean_roll_on_startup_to_false()
    {
        var policy = FilesManagerPolicyMapper.From(Definition(RequiredBag()));
        policy.CleanRollOnStartup.Should().BeFalse();
    }

    [Fact]
    public void Honours_clean_roll_on_startup_when_set()
    {
        var bag = new Dictionary<string, object?>(RequiredBag())
        {
            ["cleanRollOnStartup"] = true
        };
        var policy = FilesManagerPolicyMapper.From(Definition(bag));
        policy.CleanRollOnStartup.Should().BeTrue();
    }

    [Fact]
    public void Defaults_share_for_writing_to_false()
    {
        var policy = FilesManagerPolicyMapper.From(Definition(RequiredBag()));
        policy.ShareForWriting.Should().BeFalse();
    }

    [Fact]
    public void Defaults_path_tokens_to_null_when_absent()
    {
        var policy = FilesManagerPolicyMapper.From(Definition(RequiredBag()));
        policy.PathTokens.Should().BeNull();
    }

    [Fact]
    public void Carries_path_tokens_from_a_nested_dictionary()
    {
        var nested = new Dictionary<string, object?>
        {
            ["service"] = "api",
            ["tenant"]  = "acme"
        };
        var bag = new Dictionary<string, object?>(RequiredBag()) { ["pathTokens"] = nested };
        var policy = FilesManagerPolicyMapper.From(Definition(bag));
        policy.PathTokens.Should().NotBeNull();
        policy.PathTokens!["service"].Should().Be("api");
        policy.PathTokens!["tenant"].Should().Be("acme");
    }

    // ── Custom-interval timing ────────────────────────────────────

    [Fact]
    public void Honours_start_minute_and_capture_duration_when_set()
    {
        var bag = new Dictionary<string, object?>(RequiredBag())
        {
            ["rollingLogsEnabled"]      = true,
            ["rollingInterval"]         = "custom",
            ["startMinute"]             = 15,
            ["captureDurationMinutes"]  = 30
        };
        var policy = FilesManagerPolicyMapper.From(Definition(bag));
        policy.Interval.Should().Be(RollingInterval.Custom);
        policy.StartMinute.Should().Be(15);
        policy.CaptureDurationMinutes.Should().Be(30);
    }

    // ── Round-trip (every documented mmpform key) ────────────────

    // ── Silent-drop guard ─────────────────────────────────────────

    [Fact]
    public void Mapper_sets_every_FilesManagerPolicy_constructor_parameter()
    {
        // Reflection-based contract test. If a future engineer adds a
        // new parameter to FilesManagerPolicy without also wiring it
        // through FilesManagerPolicyMapper, the policy field would
        // silently default to whatever the record's own default is —
        // and the mapper would drop any bag value the dashboard
        // sent. This test fails the moment that drift opens, so the
        // reviewer sees the gap before the bag-→-policy contract
        // breaks in production.
        //
        // Every value in the bag below MUST differ from the matching
        // policy default — otherwise the per-property "differs from
        // default" check yields a false negative. logExtension is
        // ".ndjson" instead of ".log" for that reason.
        var bag = new Dictionary<string, object?>
        {
            ["logDirectory"]            = "/var/log/herald",
            ["logFileTemplate"]         = "audit-{date}.{seq}",
            ["logExtension"]            = "ndjson",
            ["activeFileName"]          = "current.log",
            ["rollingLogsEnabled"]      = true,
            ["rollingInterval"]         = "daily",
            ["maxFileSize"]             = "10MB",
            ["maxMessagesPerFile"]      = 50_000,
            ["maxRetainedFiles"]        = 30,
            ["totalSizeCap"]            = "5GB",
            ["retentionDays"]           = 14,
            ["namePattern"]             = "yyyy-MM-dd",
            ["startMinute"]             = 5,
            ["captureDurationMinutes"]  = 45,
            ["compression"]             = "gzip",
            ["diskFull"]                = "drop",
            ["cleanRollOnStartup"]      = true,
            ["shareForWriting"]         = true,
            ["locale"]                  = "en-US",
            ["pathTokens"]              = new Dictionary<string, object?> { ["service"] = "api" }
        };
        var policy = FilesManagerPolicyMapper.From(Definition(bag));

        // Limit reflection to the record's primary constructor
        // parameters — body-defined properties (RequiresRolling,
        // RequiresRetention) are computed and not part of the
        // mapper's responsibility.
        var ctor = typeof(FilesManagerPolicy).GetConstructors()[0];
        var ctorParamNames = new System.Collections.Generic.HashSet<string>();
        foreach (var param in ctor.GetParameters())
        {
            ctorParamNames.Add(param.Name!);
        }

        // Compare each ctor-parameter property to the same property
        // on a sentinel-default policy. If actual differs from
        // default, the mapper read this key from the bag.
        var defaultPolicy = new FilesManagerPolicy(
            Directory: "_unset_",
            FileNameTemplate: "_unset_");
        var mapperCovered = new System.Collections.Generic.List<string>();
        foreach (var prop in typeof(FilesManagerPolicy).GetProperties())
        {
            if (!ctorParamNames.Contains(prop.Name)) continue;
            var actual = prop.GetValue(policy);
            var defaultValue = prop.GetValue(defaultPolicy);
            if (!Equals(actual, defaultValue)) mapperCovered.Add(prop.Name);
        }

        // Every constructor parameter on FilesManagerPolicy must be
        // covered. If this list grows because the policy gained a
        // new parameter, the test fails and the reviewer sees the
        // gap before the bag-→-policy contract breaks in production.
        var expected = new[]
        {
            nameof(FilesManagerPolicy.Directory),
            nameof(FilesManagerPolicy.FileNameTemplate),
            nameof(FilesManagerPolicy.Extension),
            nameof(FilesManagerPolicy.ActiveFileName),
            nameof(FilesManagerPolicy.Interval),
            nameof(FilesManagerPolicy.MaxBytesPerFile),
            nameof(FilesManagerPolicy.MaxMessagesPerFile),
            nameof(FilesManagerPolicy.MaxRetainedFiles),
            nameof(FilesManagerPolicy.TotalSizeCapBytes),
            nameof(FilesManagerPolicy.RetentionDays),
            nameof(FilesManagerPolicy.Compression),
            nameof(FilesManagerPolicy.DiskFull),
            nameof(FilesManagerPolicy.StartMinute),
            nameof(FilesManagerPolicy.CaptureDurationMinutes),
            nameof(FilesManagerPolicy.FileNameSuffix),
            nameof(FilesManagerPolicy.Locale),
            nameof(FilesManagerPolicy.ShareForWriting),
            nameof(FilesManagerPolicy.CleanRollOnStartup),
            nameof(FilesManagerPolicy.PathTokens),
        };
        mapperCovered.Should().BeEquivalentTo(expected,
            "every parameter the mapper sets must be in this list, and every parameter " +
            "on FilesManagerPolicy must be set by the mapper. If this fails, either the " +
            "policy gained a parameter and the mapper hasn't been taught to read it yet, " +
            "or the mapper picked up a parameter that this list hasn't been told about.");
    }

    [Fact]
    public void Round_trips_a_fully_populated_v2_bag_into_the_policy()
    {
        // This is the canonical "the bag the dashboard sends produces
        // the policy the runtime uses" guarantee. Every documented
        // mmpform key is set; every policy field must reflect it.
        var bag = new Dictionary<string, object?>
        {
            ["logDirectory"]            = "/var/log/herald",
            ["logFileTemplate"]         = "audit-{date}.{seq}",
            ["logExtension"]            = "log",
            ["activeFileName"]          = "current.log",
            ["rollingLogsEnabled"]      = true,
            ["rollingInterval"]         = "daily",
            ["maxFileSize"]             = "10MB",
            ["maxMessagesPerFile"]      = 50_000,
            ["maxRetainedFiles"]        = 30,
            ["totalSizeCap"]            = "5GB",
            ["retentionDays"]           = 14,
            ["namePattern"]             = "yyyy-MM-dd",
            ["startMinute"]             = 0,
            ["captureDurationMinutes"]  = 60,
            ["compression"]             = "gzip",
            ["diskFull"]                = "drop",
            ["cleanRollOnStartup"]      = true,
            ["shareForWriting"]         = false,
            ["locale"]                  = "en-US",
            ["pathTokens"]              = new Dictionary<string, object?> { ["service"] = "api" }
        };
        var policy = FilesManagerPolicyMapper.From(Definition(bag));

        policy.Directory.Should().Be("/var/log/herald");
        policy.FileNameTemplate.Should().Be("audit-{date}.{seq}");
        policy.Extension.Should().Be(".log");
        policy.ActiveFileName.Should().Be("current.log");
        policy.Interval.Should().Be(RollingInterval.Daily);
        policy.MaxBytesPerFile.Should().Be(10L * 1024 * 1024);
        policy.MaxMessagesPerFile.Should().Be(50_000);
        policy.MaxRetainedFiles.Should().Be(30);
        policy.TotalSizeCapBytes.Should().Be(5L * 1024 * 1024 * 1024);
        policy.RetentionDays.Should().Be(14);
        policy.FileNameSuffix.Should().Be("yyyy-MM-dd");
        policy.StartMinute.Should().Be(0);
        policy.CaptureDurationMinutes.Should().Be(60);
        policy.Compression.Should().Be(CompressionMode.Gzip);
        policy.DiskFull.Should().Be(DiskFullStrategy.Drop);
        policy.CleanRollOnStartup.Should().BeTrue();
        policy.ShareForWriting.Should().BeFalse();
        policy.Locale.Should().Be("en-US");
        policy.PathTokens.Should().NotBeNull();
        policy.PathTokens!["service"].Should().Be("api");
    }
}
