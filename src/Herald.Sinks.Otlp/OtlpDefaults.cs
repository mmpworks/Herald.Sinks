// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

namespace Herald.Sinks.Otlp;

/// <summary>
/// Default values for OTLP log sink configuration.
/// </summary>
public static class OtlpDefaults
{
    /// <summary>OTLP resource attribute key for the service name.</summary>
    public const string ServiceNameKey = "service.name";

    /// <summary>Default service name when none is provided.</summary>
    public const string UnknownService = "unknown_service";
}
