// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.BigQuery.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.BigQuery;

public static class BigQuerySinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new BigQueryLogSinkProvider());
    }
}
