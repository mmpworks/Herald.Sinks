// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

namespace Herald.Sinks.Graylog;

/// <summary>GELF wire transport for Graylog delivery.</summary>
public enum GraylogTransport
{
    Http = 0,
    Tcp = 1,
}
