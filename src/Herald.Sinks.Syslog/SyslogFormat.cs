// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

namespace Herald.Sinks.Syslog;

/// <summary>
/// Syslog wire-format selector. RFC 5424 is the modern format and what
/// most modern collectors (Graylog, Logstash, Fluentd) prefer. RFC 3164
/// is the older BSD format still spoken by many appliance-style
/// collectors; opt in when the receiver requires it.
/// </summary>
public enum SyslogFormat
{
    Rfc5424 = 0,
    Rfc3164 = 1,
}
