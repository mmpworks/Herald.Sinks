// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.GoogleCloudPubSub.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.GoogleCloudPubSub;

public static class GoogleCloudPubSubSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new GoogleCloudPubSubLogSinkProvider());
    }
}
