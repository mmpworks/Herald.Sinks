// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

namespace Herald.Sinks.Syslog;

/// <summary>
/// Wire transport for syslog delivery. UDP (port 514) is the original
/// default — lossy but cheap. TCP (port 514 or 601) adds reliability and
/// is required for payloads larger than an IP datagram; TCP framing uses
/// octet counting per RFC 6587.
/// </summary>
public enum SyslogTransport
{
    Udp = 0,
    Tcp = 1,
}
