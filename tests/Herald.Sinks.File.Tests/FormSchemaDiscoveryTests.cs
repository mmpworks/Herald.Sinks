// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using FluentAssertions;
using Herald.Sinks.File.Providers;
using MMP.Herald.Routing;
using Xunit;

namespace Herald.Sinks.File.Tests;

/// <summary>
/// Verifies the per-kind form-schema discovery contract that
/// <see cref="MMP.Herald.Routing.ILogSinkProvider"/> exposes through its
/// default <c>GetFormSchemaText()</c> implementation. Each provider in
/// this package ships its own <c>configuration-{SinkKind}.mmpform</c>
/// resource and the default lookup picks the right one off the
/// assembly. Plugin sink authors who follow the same convention get the
/// same behaviour for free, so a regression here would silently break
/// every third-party sink that ships per-kind forms.
/// </summary>
public sealed class FormSchemaDiscoveryTests
{
    [Fact]
    public void TextFileSinkProvider_returns_its_per_kind_mmpform()
    {
        ILogSinkProvider provider = new TextFileSinkProvider();

        var formText = provider.GetFormSchemaText();

        formText.Should().NotBeNullOrEmpty();
        // Sentinel from configuration-text_file.mmpform — the container
        // header. Picking up json_file's form would break this assertion.
        formText!.Should().Contain("Text File");
        formText.Should().NotContain("Writes NDJSON log files to disk");
    }

    [Fact]
    public void JsonFileSinkProvider_returns_its_per_kind_mmpform()
    {
        ILogSinkProvider provider = new JsonFileSinkProvider();

        var formText = provider.GetFormSchemaText();

        formText.Should().NotBeNullOrEmpty();
        // Sentinel from configuration-json_file.mmpform — the container
        // header. Picking up text_file's form would break this assertion.
        formText!.Should().Contain("JSON File");
        formText.Should().Contain("Writes NDJSON log files to disk");
    }

    [Fact]
    public void Both_providers_use_the_v2_binding_names()
    {
        // Locks down the wire contract the management API + dashboard
        // share. If a sink author renames a binding the apply path no
        // longer recognises, the form silently no-ops on commit — see
        // FileSinkConfig and CommitFullFileSinkTests in Herald.Core for
        // the matching server-side guarantees.
        var textForm = ((ILogSinkProvider)new TextFileSinkProvider()).GetFormSchemaText();
        var jsonForm = ((ILogSinkProvider)new JsonFileSinkProvider()).GetFormSchemaText();

        // Shared bindings — both forms expose these under the same name.
        foreach (var form in new[] { textForm, jsonForm })
        {
            form.Should().Contain("{logDirectory}");
            form.Should().Contain("{logFileTemplate}");
            form.Should().Contain("{logExtension}");
            form.Should().Contain("{rollingLogsEnabled}");
            form.Should().Contain("{rollingInterval}");
            form.Should().Contain("{maxFileSize}");
            form.Should().Contain("{maxRetainedFiles}");
            form.Should().Contain("{totalSizeCap}");
            form.Should().Contain("{retentionDays}");
        }

        // text_file moved to the shorter `namePattern` binding;
        // json_file still uses the original `fileNamePattern`. The
        // file-sink runtime helper accepts either spelling so both
        // sinks stay compatible during the transition.
        textForm!.Should().Contain("{namePattern}");
        jsonForm!.Should().Contain("{fileNamePattern}");
    }
}
