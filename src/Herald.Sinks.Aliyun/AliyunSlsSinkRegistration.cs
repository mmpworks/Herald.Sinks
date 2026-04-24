// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.Aliyun.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.Aliyun;

public static class AliyunSlsSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new AliyunSlsLogSinkProvider());
    }
}
