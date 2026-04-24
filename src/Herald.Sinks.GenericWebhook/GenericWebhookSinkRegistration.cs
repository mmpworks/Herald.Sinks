// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using Herald.Sinks.GenericWebhook.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.GenericWebhook;

/// <summary>
/// Opt-in registration helper for consumers that want the generic
/// webhook sink without constructing the provider themselves.
/// </summary>
public static class GenericWebhookSinkRegistration
{
    /// <summary>
    /// Register the generic webhook sink provider without rules.
    /// Every event matching the pipeline's routing is POSTed.
    /// </summary>
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new GenericWebhookSinkProvider());
    }

    /// <summary>
    /// Register the generic webhook sink provider with a rules engine.
    /// Only events matching at least one rule — and passing cooldown
    /// — are POSTed. This replaces the <c>QuickLogBuilder.WithWebhookSink(url, rules)</c>
    /// overload that was removed from Core during the sink extraction.
    /// </summary>
    public static void RegisterWithRules(LogSinkProviderRegistry registry, IReadOnlyList<WebhookRule> rules)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(rules);
        registry.Register(new GenericWebhookSinkProvider(rules));
    }
}
