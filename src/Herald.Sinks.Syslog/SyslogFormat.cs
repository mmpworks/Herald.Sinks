// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
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
