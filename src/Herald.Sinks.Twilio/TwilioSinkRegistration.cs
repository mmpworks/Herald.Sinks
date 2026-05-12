// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using Herald.Sinks.Twilio.Providers;
using MMP.Herald.Routing;

namespace Herald.Sinks.Twilio;

public static class TwilioSinkRegistration
{
    public static void RegisterAll(LogSinkProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new TwilioLogSinkProvider());
    }
}
