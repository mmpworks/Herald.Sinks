// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
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
