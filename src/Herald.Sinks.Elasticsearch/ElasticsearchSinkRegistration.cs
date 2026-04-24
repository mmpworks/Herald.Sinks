// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.Elasticsearch.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.Elasticsearch;

/// <summary>
/// Opt-in registration helper for consumers that want the Elasticsearch
/// sink without constructing the provider themselves.
/// </summary>
public static class ElasticsearchSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new ElasticsearchSinkProvider());
    }
}
