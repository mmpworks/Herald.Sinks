// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Herald.Sinks.Contract.Tests;

/// <summary>
/// Repo-wide naming-contract invariants. Reads every Herald.Sinks csproj
/// and CAPABILITY.yaml on disk and asserts the shape that the Herald.OSS
/// 1.0 distribution is committed to:
///
///   * PackageId == AssemblyName == namespace root (no MMP. prefix)
///   * Sink PackageIds start with Herald.Sinks.
///   * CAPABILITY.yaml's package_id mirrors its csproj's PackageId
///   * Per-sink csprojs do NOT redeclare a Herald.Core reference —
///     Directory.Build.props owns the conditional ProjectReference /
///     PackageReference pair.
///
/// A failure here means someone copied a sink template and forgot to
/// align the names. Cheap, fast, catches drift before it ships.
/// </summary>
public sealed class NamingContractTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Herald.Sinks.slnx")))
            {
                dir = dir.Parent;
            }
            dir.Should().NotBeNull("the test runner must launch from inside Modules/Herald.Sinks");
            return dir!.FullName;
        }
    }

    private static IEnumerable<string> SinkCsprojs() =>
        Directory.EnumerateFiles(
            Path.Combine(RepoRoot, "src"),
            "Herald.Sinks.*.csproj",
            SearchOption.AllDirectories);

    private static IEnumerable<string> CapabilityYamls() =>
        Directory.EnumerateFiles(
            Path.Combine(RepoRoot, "src"),
            "CAPABILITY.yaml",
            SearchOption.AllDirectories);

    /// <summary>The repo carries the canonical sink set we expect.</summary>
    [Fact]
    public void Repo_carries_sinks()
    {
        SinkCsprojs().Should().NotBeEmpty(
            "the Herald.Sinks repo must ship at least one sink");
    }

    /// <summary>Every sink csproj declares PackageId, AssemblyName, and they match.</summary>
    [Fact]
    public void Every_sink_PackageId_equals_AssemblyName()
    {
        foreach (var csproj in SinkCsprojs())
        {
            var doc = XDocument.Load(csproj);
            var packageId = doc.Descendants("PackageId").FirstOrDefault()?.Value;
            var assemblyName = doc.Descendants("AssemblyName").FirstOrDefault()?.Value;
            var relPath = Path.GetRelativePath(RepoRoot, csproj);

            packageId.Should().NotBeNullOrEmpty(
                $"{relPath} must declare <PackageId>");
            assemblyName.Should().NotBeNullOrEmpty(
                $"{relPath} must declare <AssemblyName>");
            packageId.Should().Be(assemblyName,
                $"{relPath}: PackageId and AssemblyName must match per the 1.0 naming contract");
        }
    }

    /// <summary>Every sink PackageId starts with Herald.Sinks. — no MMP. prefix.</summary>
    [Fact]
    public void Every_sink_PackageId_starts_with_Herald_Sinks_prefix()
    {
        foreach (var csproj in SinkCsprojs())
        {
            var doc = XDocument.Load(csproj);
            var packageId = doc.Descendants("PackageId").FirstOrDefault()?.Value ?? string.Empty;
            var relPath = Path.GetRelativePath(RepoRoot, csproj);

            packageId.Should().StartWith("Herald.Sinks.",
                $"{relPath}: 1.0 contract drops the MMP. prefix from public-facing names");
            packageId.Should().NotStartWith("MMP.",
                $"{relPath}: the MMP. vendor prefix lives in metadata (NOTICE + Copyright), not in PackageId");
        }
    }

    /// <summary>Every per-sink csproj is free of a direct Herald.Core reference — Directory.Build.props owns it.</summary>
    /// <remarks>
    /// Exempted: a csproj that sets <c>HeraldSinkCustomCoreRef=true</c> is opting out
    /// of the Directory.Build.props injection (custom-fork experiments, etc.) and is
    /// expected to declare its own Core reference. The opt-out flag must be present
    /// in the csproj text for the exemption to apply.
    /// </remarks>
    [Fact]
    public void Per_sink_csproj_does_not_redeclare_Herald_Core_reference()
    {
        foreach (var csproj in SinkCsprojs())
        {
            var text = File.ReadAllText(csproj);
            if (text.Contains("<HeraldSinkCustomCoreRef>true</HeraldSinkCustomCoreRef>"))
            {
                continue;
            }
            var relPath = Path.GetRelativePath(RepoRoot, csproj);

            text.Should().NotContain("Herald.Core.csproj",
                $"{relPath}: Herald.Core reference belongs in Directory.Build.props, " +
                "not per-sink. The conditional ProjectReference (monorepo) / " +
                "PackageReference Herald.OSS (standalone) pair lives at " +
                "$(MSBuildThisFileDirectory)Directory.Build.props. " +
                "If this csproj truly needs to override, set " +
                "<HeraldSinkCustomCoreRef>true</HeraldSinkCustomCoreRef> to opt out.");
        }
    }

    /// <summary>Every CAPABILITY.yaml's package_id mirrors its csproj's PackageId.</summary>
    [Fact]
    public void Every_capability_yaml_package_id_mirrors_csproj()
    {
        foreach (var yamlPath in CapabilityYamls())
        {
            var sinkDir = new FileInfo(yamlPath).Directory!;
            var csprojPath = Path.Combine(sinkDir.FullName, $"{sinkDir.Name}.csproj");
            if (!File.Exists(csprojPath))
            {
                // Migration-guide manifests live in directories without a
                // csproj. Skip; their package_id is null by design.
                continue;
            }

            var manifest = LoadYaml(yamlPath);
            var manifestPkgId = manifest.Children.TryGetValue("package_id", out var pkg)
                ? (pkg as YamlScalarNode)?.Value
                : null;
            if (manifestPkgId is null) continue; // migration-guides + null entries

            var doc = XDocument.Load(csprojPath);
            var csprojPkgId = doc.Descendants("PackageId").FirstOrDefault()?.Value;
            var relYaml = Path.GetRelativePath(RepoRoot, yamlPath);

            csprojPkgId.Should().NotBeNullOrEmpty(
                $"{relYaml}: companion csproj must declare PackageId");
            manifestPkgId.Should().Be(csprojPkgId,
                $"{relYaml}: package_id must mirror the csproj's <PackageId>");
        }
    }

    /// <summary>Every CAPABILITY.yaml's `name` field equals its directory name.</summary>
    [Fact]
    public void Every_capability_yaml_name_matches_directory()
    {
        foreach (var yamlPath in CapabilityYamls())
        {
            var sinkDir = new FileInfo(yamlPath).Directory!;
            var manifest = LoadYaml(yamlPath);
            var name = manifest.Children.TryGetValue("name", out var n)
                ? (n as YamlScalarNode)?.Value
                : null;
            var relYaml = Path.GetRelativePath(RepoRoot, yamlPath);

            name.Should().NotBeNullOrEmpty($"{relYaml}: `name` field is required");
            name.Should().Be(sinkDir.Name,
                $"{relYaml}: `name` must match the directory");
        }
    }

    /// <summary>Directory.Build.props carries the conditional Herald.OSS reference pair.</summary>
    [Fact]
    public void DirectoryBuildProps_owns_the_Herald_OSS_reference()
    {
        var propsPath = Path.Combine(RepoRoot, "Directory.Build.props");
        File.Exists(propsPath).Should().BeTrue("Directory.Build.props must exist at repo root");

        var text = File.ReadAllText(propsPath);
        text.Should().Contain("<HeraldCoreVersion>",
            "the Herald.OSS NuGet version must be pinned in Directory.Build.props");
        text.Should().Contain("Exists('$(MSBuildThisFileDirectory)..\\Core\\Herald.Core.csproj')",
            "monorepo path must use ProjectReference to live Core source");
        text.Should().Contain("PackageReference Include=\"Herald.OSS\"",
            "standalone path must fall back to the Herald.OSS NuGet package");
    }

    /// <summary>Both branches of the conditional reference exist — drop one and the other branch silently stops working.</summary>
    [Fact]
    public void DirectoryBuildProps_has_both_monorepo_and_standalone_branches()
    {
        var propsPath = Path.Combine(RepoRoot, "Directory.Build.props");
        var text = File.ReadAllText(propsPath);

        // Positive condition (monorepo): Exists(...) ItemGroup with a ProjectReference.
        text.Should().Contain("Condition=\"Exists('$(MSBuildThisFileDirectory)..\\Core\\Herald.Core.csproj')",
            "the monorepo branch (Exists condition + ProjectReference) must be present");
        // Negative condition (standalone): !Exists(...) ItemGroup with a PackageReference to Herald.OSS.
        text.Should().Contain("Condition=\"!Exists('$(MSBuildThisFileDirectory)..\\Core\\Herald.Core.csproj')",
            "the standalone branch (!Exists condition + PackageReference) must be present — " +
            "dropping it makes a fresh clone of this repo silently fail to find Core");
    }

    /// <summary>Standalone PackageReference targets exactly the literal Herald.OSS (typo-guard).</summary>
    [Fact]
    public void Standalone_PackageReference_targets_Herald_OSS_exactly()
    {
        var propsPath = Path.Combine(RepoRoot, "Directory.Build.props");
        var text = File.ReadAllText(propsPath);

        text.Should().Contain("PackageReference Include=\"Herald.OSS\" Version=\"$(HeraldCoreVersion)\"",
            "the standalone PackageReference must target exactly the literal `Herald.OSS` " +
            "with the version coming from the $(HeraldCoreVersion) pin");
        text.Should().NotContain("PackageReference Include=\"Herald.Oss\"",
            "casing typo — NuGet is case-insensitive on Windows but case-sensitive on Linux");
        text.Should().NotContain("PackageReference Include=\"Herald.Core\"",
            "the package ID is Herald.OSS, not Herald.Core — a search/replace mistake would land here");
    }

    /// <summary>HeraldCoreVersion in Directory.Build.props matches Core's actual published Version.</summary>
    [Fact]
    public void HeraldCoreVersion_matches_Core_csproj_Version()
    {
        var coreCsprojPath = Path.GetFullPath(
            Path.Combine(RepoRoot, "..", "Core", "Herald.Core.csproj"));
        if (!File.Exists(coreCsprojPath))
        {
            // Standalone clone — Core isn't here; nothing to cross-check. The standalone
            // branch is what kicks in at build time. Skip silently.
            return;
        }

        var coreDoc = XDocument.Load(coreCsprojPath);
        var coreVersion = coreDoc.Descendants("Version").FirstOrDefault()?.Value;
        coreVersion.Should().NotBeNullOrEmpty("Herald.Core.csproj must declare <Version>");

        var propsText = File.ReadAllText(Path.Combine(RepoRoot, "Directory.Build.props"));
        var match = System.Text.RegularExpressions.Regex.Match(
            propsText,
            @"<HeraldCoreVersion>([^<]+)</HeraldCoreVersion>");
        match.Success.Should().BeTrue("Directory.Build.props must pin <HeraldCoreVersion>");

        match.Groups[1].Value.Should().Be(coreVersion,
            "the Directory.Build.props pin and Core's actual Version must agree; drift " +
            "means a standalone clone restores a different version than the monorepo builds against");
    }

    /// <summary>The sweep helper script that strips per-sink Core refs still exists.</summary>
    [Fact]
    public void Strip_core_projectref_script_still_documents_the_pattern()
    {
        var scriptPath = Path.Combine(RepoRoot, "tools", "strip-core-projectref.py");
        File.Exists(scriptPath).Should().BeTrue(
            "tools/strip-core-projectref.py is the canonical sweep that strips " +
            "any new sink's per-csproj Herald.Core reference. Keep the script so " +
            "future sink-template imports stay aligned with the contract.");
    }

    // ── helpers ──────────────────────────────────────────────────────

    private static YamlMappingNode LoadYaml(string path)
    {
        using var reader = new StreamReader(path);
        var yamlStream = new YamlStream();
        yamlStream.Load(reader);
        var root = (YamlMappingNode)yamlStream.Documents[0].RootNode;
        return root;
    }
}
