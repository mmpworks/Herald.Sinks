// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Services;

namespace Herald.Sinks.Email;

/// <summary>
/// Sink that emails log events via SMTP using MailKit. Drop-in for
/// Serilog.Sinks.Email. Sized for high-severity alerts — pair with a
/// warn+ or error+ level filter, otherwise mailbox flooding becomes
/// the bug you're trying to solve.
/// </summary>
/// <remarks>
/// <para>
/// One LogBatch becomes one email. Multi-event batches concatenate
/// into a plain-text body so a single alert digest can carry several
/// events. The subject line is a configurable template with one
/// placeholder, <c>{level}</c>, that resolves to the highest-severity
/// level in the batch.
/// </para>
/// <para>
/// MailKit handles modern SMTP including STARTTLS, OAuth2, and
/// implicit TLS. The constructor takes the SMTP host, port, optional
/// credentials, and a TLS-mode toggle.
/// </para>
/// </remarks>
public sealed class EmailLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly string? _username;
    private readonly string? _password;
    private readonly SecureSocketOptions _tls;
    private readonly MailboxAddress _from;
    private readonly IReadOnlyList<MailboxAddress> _to;
    private readonly string _subjectTemplate;

    public EmailLogSink(
        string smtpHost,
        int smtpPort,
        string fromAddress,
        IEnumerable<string> toAddresses,
        string? username = null,
        string? password = null,
        bool useStartTls = true,
        string subjectTemplate = "[Herald {level}] log alert")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(smtpHost);
        ArgumentException.ThrowIfNullOrWhiteSpace(fromAddress);
        ArgumentNullException.ThrowIfNull(toAddresses);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectTemplate);
        if (smtpPort is <= 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(smtpPort));

        _host = smtpHost;
        _port = smtpPort;
        _username = username;
        _password = password;
        _tls = useStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
        _from = MailboxAddress.Parse(fromAddress);

        var recipients = new List<MailboxAddress>();
        foreach (var address in toAddresses)
        {
            if (!string.IsNullOrWhiteSpace(address))
                recipients.Add(MailboxAddress.Parse(address));
        }
        if (recipients.Count == 0)
            throw new ArgumentException("At least one recipient is required.", nameof(toAddresses));
        _to = recipients;

        _subjectTemplate = subjectTemplate;
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        LogBatch(new[] { logEvent });
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        var highestLevel = HighestLevelKey(events);
        var subject = _subjectTemplate.Replace("{level}", highestLevel, StringComparison.Ordinal);
        var body = BuildBody(events);

        using var message = new MimeMessage();
        message.From.Add(_from);
        foreach (var recipient in _to) message.To.Add(recipient);
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var smtp = new SmtpClient();
        smtp.Connect(_host, _port, _tls);
        if (_username is not null && _password is not null)
        {
            smtp.Authenticate(_username, _password);
        }
        smtp.Send(message);
        smtp.Disconnect(quit: true);
    }

    public void Dispose()
    {
        // SmtpClient is created and disposed per Send call; nothing
        // long-lived to release here.
    }

    private static string HighestLevelKey(IReadOnlyList<LogEvent> events)
    {
        // Coarse ordering; "security" is the worst we recognise. The
        // ordering is consistent with MapSeverity in other sinks so a
        // batch containing one error + 9 infos shows up labelled error.
        var ranking = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "trace", 0 }, { "debug", 1 }, { "info", 2 }, { "notice", 3 },
            { "success", 3 }, { "warn", 4 }, { "error", 5 }, { "critical", 6 },
            { "security", 7 },
        };

        var best = "info";
        var bestRank = -1;
        foreach (var evt in events)
        {
            if (ranking.TryGetValue(evt.Level.Key, out var r) && r > bestRank)
            {
                bestRank = r;
                best = evt.Level.Key;
            }
        }
        return best;
    }

    private static string BuildBody(IReadOnlyList<LogEvent> events)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Herald log alert — {events.Count} event(s).").AppendLine();

        foreach (var evt in events)
        {
            sb.Append('[').Append(evt.TimeUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture))
              .Append("] [").Append(evt.Level.Key).Append("] [").Append(evt.Category.Value).Append("] ")
              .AppendLine(evt.Message ?? string.Empty);

            if (evt.Properties is not null && evt.Properties.Count > 0)
            {
                foreach (var prop in evt.Properties)
                {
                    sb.Append("  ").Append(prop.Name).Append('=').AppendLine(prop.ResolvedValue?.ToString() ?? string.Empty);
                }
            }

            if (evt.Context.TryGetValue(LogContextKeys.Exception, out var v) && v is Exception ex)
            {
                sb.AppendLine().AppendLine(ex.ToString());
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
