// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.AwsCloudWatch.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.AwsCloudWatch;

/// <summary>
/// Opt-in registration helper for the AwsCloudWatch sink.
/// </summary>
public static class AwsCloudWatchSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new AwsCloudWatchLogSinkProvider());
    }
}
