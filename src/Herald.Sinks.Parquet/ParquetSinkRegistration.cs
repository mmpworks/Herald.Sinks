// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.Parquet.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.Parquet;

/// <summary>
/// Opt-in registration helper for the Parquet sink.
/// </summary>
public static class ParquetSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new ParquetLogSinkProvider());
    }
}
