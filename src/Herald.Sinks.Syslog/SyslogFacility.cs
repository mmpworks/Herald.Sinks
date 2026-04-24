// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

namespace Herald.Sinks.Syslog;

/// <summary>
/// Syslog facility codes per RFC 5424 §6.2.1. The numeric values are
/// part of the wire format (<c>facility * 8 + severity</c>) so do not
/// renumber. Defaults to <see cref="User"/> for application-level logs.
/// </summary>
public enum SyslogFacility
{
    Kernel = 0,
    User = 1,
    Mail = 2,
    Daemon = 3,
    Auth = 4,
    Syslog = 5,
    Lpr = 6,
    News = 7,
    Uucp = 8,
    Cron = 9,
    AuthPriv = 10,
    Ftp = 11,
    Ntp = 12,
    Audit = 13,
    Alert = 14,
    ClockDaemon = 15,
    Local0 = 16,
    Local1 = 17,
    Local2 = 18,
    Local3 = 19,
    Local4 = 20,
    Local5 = 21,
    Local6 = 22,
    Local7 = 23,
}
