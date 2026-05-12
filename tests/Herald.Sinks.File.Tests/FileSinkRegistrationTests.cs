// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using FluentAssertions;
using Herald.Sinks.File;
using Herald.Sinks.File.Providers;
using MMP.Herald.Quick;
using MMP.Herald.Routing;
using MMP.Herald.Services;
using Xunit;

namespace Herald.Sinks.File.Tests;

public sealed class FileSinkRegistrationTests
{
    [Fact]
    public void RegisterAll_registers_text_and_json_file_providers()
    {
        var registry = new LogSinkProviderRegistry();

        FileSinkRegistration.RegisterAll(registry);

        registry.Resolve(KnownSinkKinds.TextFile).Should().BeOfType<TextFileSinkProvider>();
        registry.Resolve(KnownSinkKinds.JsonFile).Should().BeOfType<JsonFileSinkProvider>();
    }

    [Fact]
    public void RegisterAll_is_idempotent()
    {
        var registry = new LogSinkProviderRegistry();

        FileSinkRegistration.RegisterAll(registry);
        FileSinkRegistration.RegisterAll(registry);

        registry.Resolve(KnownSinkKinds.TextFile).Should().BeOfType<TextFileSinkProvider>();
        registry.Resolve(KnownSinkKinds.JsonFile).Should().BeOfType<JsonFileSinkProvider>();
    }

    [Fact]
    public void WithFileSinkProviders_returns_same_builder_for_chaining()
    {
        var builder = QuickLogBuilder.Create("herald-sinks-file-chain");

        var result = builder.WithFileSinkProviders();

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithFileSinkProviders_lets_QuickLogBuilder_resolve_a_text_file_sink_at_Build()
    {
        // End-to-end smoke: a successful Build() means JsonConfiguredLoggingBootstrapFactory
        // resolved the text-file sink kind through the registry. A missing
        // provider would surface as KeyNotFoundException from the registry.
        var build = QuickLogBuilder.Create("herald-sinks-file-text-smoke")
            .WithFileSinkProviders()
            .WithFileSink("logs/herald-sinks-file-text-smoke.log")
            .Build();

        build.Should().NotBeNull();
        build.ConfigJson.Should().Contain("text_file");
    }

    [Fact]
    public void WithFileSinkProviders_lets_QuickLogBuilder_resolve_a_json_file_sink_at_Build()
    {
        var build = QuickLogBuilder.Create("herald-sinks-file-json-smoke")
            .WithFileSinkProviders()
            .WithFileSink("logs/herald-sinks-file-json-smoke.ndjson")
            .Build();

        build.Should().NotBeNull();
        build.ConfigJson.Should().Contain("json_file");
    }
}
