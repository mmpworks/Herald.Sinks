// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;

namespace Herald.Sinks.ApplicationInsightsHttp;

/// <summary>
/// Parses an Application Insights connection string into its
/// instrumentation key + ingestion endpoint components.
/// </summary>
/// <remarks>
/// AI connection strings follow a semicolon-delimited key=value shape:
///   <c>InstrumentationKey=&lt;guid&gt;;IngestionEndpoint=https://&lt;region&gt;.in.applicationinsights.azure.com/;LiveEndpoint=...;ApplicationId=...</c>
///
/// For backwards compatibility callers can also pass a bare
/// instrumentation-key GUID; the ingestion endpoint then defaults to
/// the classic global data collector at
/// <c>https://dc.services.visualstudio.com/</c>.
///
/// Only two fields matter for log ingestion — the key and the endpoint.
/// LiveEndpoint, ApplicationId, AADAudience, etc. are ignored.
/// </remarks>
public sealed record ApplicationInsightsConnectionString(
    string InstrumentationKey,
    Uri IngestionEndpoint)
{
    /// <summary>
    /// The legacy global ingestion endpoint. Used when the caller
    /// passes only an instrumentation key without a full connection
    /// string.
    /// </summary>
    public const string DefaultIngestionEndpoint = "https://dc.services.visualstudio.com/";

    /// <summary>
    /// The track endpoint path appended to <see cref="IngestionEndpoint"/>.
    /// v2.1 accepts both single envelopes and batches.
    /// </summary>
    public const string TrackPath = "v2.1/track";

    /// <summary>
    /// Full URL that the sink POSTs to.
    /// </summary>
    public Uri TrackEndpoint => new(IngestionEndpoint, TrackPath);

    /// <summary>
    /// Parses a connection string or bare instrumentation key. Throws
    /// <see cref="FormatException"/> if neither shape is recognizable.
    /// </summary>
    public static ApplicationInsightsConnectionString Parse(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var trimmed = connectionString.Trim();

        // Bare GUID → legacy instrumentation-key form.
        if (Guid.TryParse(trimmed, out _))
        {
            return new ApplicationInsightsConnectionString(
                trimmed,
                new Uri(DefaultIngestionEndpoint, UriKind.Absolute));
        }

        var parts = ParseKeyValuePairs(trimmed);

        if (!parts.TryGetValue("InstrumentationKey", out var ikey) || string.IsNullOrWhiteSpace(ikey))
        {
            throw new FormatException(
                "Application Insights connection string is missing required 'InstrumentationKey' field.");
        }

        var endpoint = parts.TryGetValue("IngestionEndpoint", out var ep) && !string.IsNullOrWhiteSpace(ep)
            ? NormalizeEndpoint(ep)
            : new Uri(DefaultIngestionEndpoint, UriKind.Absolute);

        return new ApplicationInsightsConnectionString(ikey, endpoint);
    }

    private static Dictionary<string, string> ParseKeyValuePairs(string input)
    {
        // Connection string sample:
        //   InstrumentationKey=abc;IngestionEndpoint=https://eastus.in...;LiveEndpoint=...
        // Values may contain '=' inside (rare, but defensively split on first '=' only).
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var segment in input.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = segment.IndexOf('=');
            if (eq <= 0) continue;

            var key = segment[..eq].Trim();
            var value = segment[(eq + 1)..].Trim();
            if (!string.IsNullOrEmpty(key))
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static Uri NormalizeEndpoint(string endpoint)
    {
        // AI connection strings sometimes include a trailing slash and
        // sometimes do not. The track path uses relative resolution so
        // the base URL must end in '/' or the final component of the
        // path gets replaced.
        var trimmed = endpoint.Trim();
        if (!trimmed.EndsWith('/'))
        {
            trimmed += "/";
        }
        return new Uri(trimmed, UriKind.Absolute);
    }
}
