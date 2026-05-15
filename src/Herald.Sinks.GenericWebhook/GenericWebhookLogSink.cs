// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Events;
using MMP.Herald.Formatting;
using MMP.Herald.Levels;
using MMP.Herald.Pipeline;

namespace Herald.Sinks.GenericWebhook;

/// <summary>
/// Generic webhook sink that POSTs formatted log events to any HTTP endpoint.
/// Supports custom headers for authentication (Bearer tokens, API keys).
///
/// When rules are configured, events are evaluated against the rule engine before sending.
/// Only events matching at least one rule (with cooldown enforcement) are delivered.
/// Without rules, all events are sent (backward-compatible behavior).
///
/// Rules engine inspired by:
///   - PagerDuty Event Rules: severity-based routing with dedup suppression
///   - Opsgenie Alert Policies: tag/message filters with notification intervals
///   - Datadog Monitors: composite conditions with re-notification intervals
///   - Grafana Notification Policies: label matchers with continue/mute options
///
/// Use with any ILogFormatter (JSON, plain text, template) or the default JsonFormatter.
/// </summary>
public sealed class GenericWebhookLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable
{
    internal static readonly HeraldEdition MinEdition = HeraldEdition.Community;

    private readonly string _url;
    private readonly ILogFormatter _formatter;
    private readonly IReadOnlyDictionary<string, string>? _headers;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;
    private readonly string _contentType;
    private readonly WebhookRuleEngine? _ruleEngine;

    public GenericWebhookLogSink(
        string url,
        ILogLevelRegistry levelRegistry,
        ILogFormatter? formatter = null,
        IReadOnlyDictionary<string, string>? headers = null,
        string contentType = "application/json",
        HttpClient? httpClient = null,
        IReadOnlyList<WebhookRule>? rules = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        _url = url;
        _formatter = formatter ?? new JsonFormatter(levelRegistry);
        _headers = headers;
        _contentType = contentType;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _ownsClient = httpClient is null;
        _ruleEngine = rules is { Count: > 0 }
            ? new WebhookRuleEngine(rules, levelRegistry)
            : null;
    }

    public override void Log(LogEvent logEvent) {
        ArgumentNullException.ThrowIfNull(logEvent);

        if (_ruleEngine is not null)
        {
            var matched = _ruleEngine.Evaluate(logEvent);
            if (matched.Count == 0) return;

            // Use payload template from first matched rule if available
            var payload = BuildPayload(logEvent, matched[0]);
            SendPayload(payload);
            return;
        }

        var formatted = _formatter.Format(logEvent);
        SendPayload(formatted);
    }

    public void LogBatch(IReadOnlyList<LogEvent> events) {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        if (_ruleEngine is not null)
        {
            // Evaluate each event individually - rules may have different cooldowns
            foreach (var evt in events)
            {
                Log(evt);
            }
            return;
        }

        var sb = new StringBuilder();
        foreach (var evt in events)
        {
            sb.AppendLine(_formatter.Format(evt));
        }

        SendPayload(sb.ToString());
    }

    public void Dispose() {
        if (_ownsClient) _httpClient.Dispose();
    }

    private string BuildPayload(LogEvent logEvent, WebhookRule matchedRule) {
        if (matchedRule.PayloadTemplate is null)
            return _formatter.Format(logEvent);

        // Template expansion: replace placeholders with event values.
        // Supports: {level}, {category}, {message}, {timestamp}, {ruleName}, {prop:Name}
        var result = matchedRule.PayloadTemplate
            .Replace("{level}", EscapeJsonValue(logEvent.Level.Key), StringComparison.Ordinal)
            .Replace("{category}", EscapeJsonValue(logEvent.Category.Value), StringComparison.Ordinal)
            .Replace("{message}", EscapeJsonValue(logEvent.Message), StringComparison.Ordinal)
            .Replace("{timestamp}", logEvent.TimeUtc.ToString("O"), StringComparison.Ordinal)
            .Replace("{ruleName}", EscapeJsonValue(matchedRule.Name), StringComparison.Ordinal);

        // Expand property references: {prop:PropertyName}
        result = ExpandPropertyPlaceholders(result, logEvent);

        return result;
    }

    /// <summary>
    /// Expand {prop:Name} placeholders with resolved property values.
    /// Uses indexOf scanning rather than regex for hot-path performance.
    /// </summary>
    private static string ExpandPropertyPlaceholders(string template, LogEvent logEvent) {
        const string prefix = "{prop:";
        var sb = new StringBuilder(template.Length);
        var pos = 0;

        while (pos < template.Length)
        {
            var start = template.IndexOf(prefix, pos, StringComparison.Ordinal);
            if (start < 0)
            {
                sb.Append(template, pos, template.Length - pos);
                break;
            }

            sb.Append(template, pos, start - pos);

            var nameStart = start + prefix.Length;
            var end = template.IndexOf('}', nameStart);
            if (end < 0)
            {
                // Unclosed placeholder - emit as-is
                sb.Append(template, start, template.Length - start);
                break;
            }

            var propName = template[nameStart..end];
            var prop = logEvent.GetProperty(propName);
            var value = prop?.ResolvedValue?.ToString() ?? "";
            sb.Append(EscapeJsonValue(value));

            pos = end + 1;
        }

        return sb.ToString();
    }

    private void SendPayload(string payload) {
        var content = new StringContent(payload, Encoding.UTF8, _contentType);
        using var request = new HttpRequestMessage(HttpMethod.Post, _url) { Content = content };

        if (_headers is not null)
        {
            foreach (var (key, value) in _headers)
            {
                if (key.Contains('\r') || key.Contains('\n') ||
                    value.Contains('\r') || value.Contains('\n'))
                    continue; // Skip headers with newlines to prevent header injection

                request.Headers.TryAddWithoutValidation(key, value);
            }
        }

        using var response = _httpClient.Send(request);
        response.EnsureSuccessStatusCode();
    }

    private static string EscapeJsonValue(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("\"", "\\\"", StringComparison.Ordinal)
             .Replace("\n", "\\n", StringComparison.Ordinal)
             .Replace("\r", "\\r", StringComparison.Ordinal);
}
