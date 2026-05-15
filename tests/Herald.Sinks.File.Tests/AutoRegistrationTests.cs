// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System.Linq;
using FluentAssertions;
using Herald.Sinks.File.Providers;
using MMP.Herald.Routing;
using Xunit;

namespace Herald.Sinks.File.Tests;

/// <summary>
/// Verifies that referencing Herald.Sinks.File alone — with no fluent
/// <c>WithFileSinkProviders()</c> call, no manual
/// <c>FileSinkRegistration.RegisterAll(...)</c>, no Server-side
/// <c>PluginLoader</c> — is enough to populate the process-wide
/// <see cref="LogSinkProviderRegistry.Default"/>.
///
/// <para>
/// The mechanism: <c>MMP.Herald.Generators.SinkAutoRegistrationGenerator</c>
/// emits a <c>[ModuleInitializer]</c> in this assembly that registers every
/// public concrete <c>ILogSinkProvider</c> with a publicly-constructable
/// ctor. The hook fires on assembly load, which happens the moment any
/// test references a type from Herald.Sinks.File. The
/// <c>using Herald.Sinks.File.Providers;</c> above is enough.
/// </para>
/// </summary>
public sealed class AutoRegistrationTests
{
    // Force-load the assembly without taking a dependency on FileSinkRegistration
    // — that helper is the manual path we want to prove unnecessary. Touching
    // the provider type symbol forces the CLR to JIT this method, which forces
    // the assembly to load, which fires the [ModuleInitializer] the generator
    // emitted.
    private static readonly System.Type _forceLoad = typeof(TextFileSinkProvider);

    [Fact]
    public void TextFile_provider_is_auto_registered_on_assembly_load()
    {
        _ = _forceLoad; // silence unused warning; the touch is the point

        LogSinkProviderRegistry.Default.Contains("text_file").Should().BeTrue(
            "Herald.Sinks.File's [ModuleInitializer] should have registered TextFileSinkProvider " +
            "into LogSinkProviderRegistry.Default on assembly load");
    }

    [Fact]
    public void JsonFile_provider_is_auto_registered_on_assembly_load()
    {
        _ = _forceLoad;

        LogSinkProviderRegistry.Default.Contains("json_file").Should().BeTrue(
            "Herald.Sinks.File's [ModuleInitializer] should have registered JsonFileSinkProvider " +
            "into LogSinkProviderRegistry.Default on assembly load");
    }

    [Fact]
    public void Resolved_provider_instance_is_the_generated_default_ctor_instance()
    {
        _ = _forceLoad;

        var provider = LogSinkProviderRegistry.Default.Resolve("text_file");

        provider.Should().BeOfType<TextFileSinkProvider>(
            "the generator emits new TextFileSinkProvider() with the default path resolver");
        provider.SinkKind.Should().Be("text_file");
    }

    [Fact]
    public void Auto_registration_works_without_any_manual_RegisterAll_call()
    {
        // This is the headline claim: `dotnet add package Herald.Sinks.File`
        // plus a single `using` is the entire developer workflow. No call to
        // FileSinkRegistration.RegisterAll, no QuickLogBuilder.WithFileSinkProviders.
        _ = _forceLoad;

        var registry = LogSinkProviderRegistry.Default;
        registry.Contains("text_file").Should().BeTrue();
        registry.Contains("json_file").Should().BeTrue();
        registry.AllProviders().Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void AllProviders_includes_both_file_providers()
    {
        _ = _forceLoad;

        var kinds = LogSinkProviderRegistry.Default.AllKinds();

        kinds.Should().Contain("text_file");
        kinds.Should().Contain("json_file");
    }
}
