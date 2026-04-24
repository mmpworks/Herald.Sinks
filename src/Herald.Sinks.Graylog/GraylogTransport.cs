// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

namespace Herald.Sinks.Graylog;

/// <summary>GELF wire transport for Graylog delivery.</summary>
public enum GraylogTransport
{
    Http = 0,
    Tcp = 1,
}
