// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.Slack.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.Slack;

/// <summary>
/// Opt-in registration helper for consumers that want the Slack
/// webhook sink without constructing the provider themselves.
/// </summary>
public static class SlackSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new SlackWebhookSinkProvider());
    }
}
