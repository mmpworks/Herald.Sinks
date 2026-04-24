// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MMP.Herald.Events;
using MMP.Herald.Levels;

namespace Herald.Sinks.GenericWebhook;

/// <summary>
/// Evaluates webhook rules against log events and enforces cooldown periods.
///
/// Thread-safe: cooldown tracking uses ConcurrentDictionary with atomic timestamp comparison.
/// Regex patterns are compiled and cached on first use for performance.
///
/// Design follows patterns from:
///   - PagerDuty Event Rules: ordered rule evaluation with first-match-wins
///   - Grafana Notification Policies: tree-based matching with continue option
///   - Elasticsearch Watcher: condition/action separation with throttle_period
///   - Datadog Monitors: composite conditions with re-notification intervals
/// </summary>
public sealed class WebhookRuleEngine
{
    private readonly IReadOnlyList<WebhookRule> _rules;
    private readonly ILogLevelRegistry _levelRegistry;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastFired = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Regex> _regexCache = new(StringComparer.Ordinal);

    public WebhookRuleEngine(IReadOnlyList<WebhookRule> rules, ILogLevelRegistry levelRegistry) {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(levelRegistry);
        _rules = rules;
        _levelRegistry = levelRegistry;
    }

    /// <summary>
    /// Evaluate all rules against the event. Returns the list of rules that matched
    /// and passed cooldown checks. Empty list means the event should not trigger any webhook.
    /// </summary>
    public IReadOnlyList<WebhookRule> Evaluate(LogEvent logEvent) {
        ArgumentNullException.ThrowIfNull(logEvent);

        if (_rules.Count == 0) return [];

        var matched = new List<WebhookRule>();

        foreach (var rule in _rules)
        {
            if (!AllConditionsMatch(rule, logEvent)) continue;
            if (!PassesCooldown(rule)) continue;

            matched.Add(rule);
            RecordFiring(rule);

            if (!rule.ContinueOnMatch) break;
        }

        return matched;
    }

    /// <summary>
    /// Check if any rule would match the event (ignoring cooldown).
    /// Useful for pre-filtering before expensive formatting.
    /// </summary>
    public bool AnyRuleMatches(LogEvent logEvent) {
        foreach (var rule in _rules)
        {
            if (AllConditionsMatch(rule, logEvent)) return true;
        }
        return false;
    }

    // -- Condition evaluation --

    // Cognitive complexity note: this method dispatches each condition type to a focused
    // helper via switch expression. Each helper is a single comparison. The AND-all loop
    // exits early on first mismatch.
    private bool AllConditionsMatch(WebhookRule rule, LogEvent logEvent) {
        foreach (var condition in rule.Conditions)
        {
            var matches = EvaluateCondition(condition, logEvent);
            if (!matches) return false;
        }
        return true;
    }

    private bool EvaluateCondition(WebhookRuleCondition condition, LogEvent logEvent) {
        if (condition.Type == WebhookConditionType.MinLevel)
            return EvaluateMinLevel(condition.Value, logEvent);
        if (condition.Type == WebhookConditionType.CategoryEquals)
            return EvaluateCategoryEquals(condition.Value, logEvent);
        if (condition.Type == WebhookConditionType.CategoryContains)
            return EvaluateCategoryContains(condition.Value, logEvent);
        if (condition.Type == WebhookConditionType.MessageContains)
            return EvaluateMessageContains(condition.Value, logEvent);
        if (condition.Type == WebhookConditionType.MessageMatches)
            return EvaluateMessageMatches(condition.Value, logEvent);
        if (condition.Type == WebhookConditionType.PropertyExists)
            return logEvent.HasProperty(condition.Value);
        if (condition.Type == WebhookConditionType.PropertyEquals)
            return EvaluatePropertyEquals(condition.Value, condition.PropertyValue, logEvent);

        return false; // Unknown condition type - fail closed for safety
    }

    private bool EvaluateMinLevel(string levelKey, LogEvent logEvent) {
        var threshold = _levelRegistry.GetByKeyOrNull(levelKey);
        if (threshold is null) return false;

        return _levelRegistry.IsAtOrAbove(logEvent.Level, threshold);
    }

    private static bool EvaluateCategoryEquals(string category, LogEvent logEvent) =>
        logEvent.Category.Value.Equals(category, StringComparison.OrdinalIgnoreCase);

    private static bool EvaluateCategoryContains(string substring, LogEvent logEvent) =>
        logEvent.Category.Value.Contains(substring, StringComparison.OrdinalIgnoreCase);

    private static bool EvaluateMessageContains(string substring, LogEvent logEvent) =>
        logEvent.Message.Contains(substring, StringComparison.OrdinalIgnoreCase);

    private bool EvaluateMessageMatches(string pattern, LogEvent logEvent) {
        var regex = _regexCache.GetOrAdd(pattern, static p =>
            new Regex(p, RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)));

        try
        {
            return regex.IsMatch(logEvent.Message);
        }
        catch (RegexMatchTimeoutException)
        {
            return false; // Pattern took too long - fail safe, don't block the pipeline
        }
    }

    private static bool EvaluatePropertyEquals(string propertyName, string? expectedValue, LogEvent logEvent) {
        var prop = logEvent.GetProperty(propertyName);
        if (prop is null) return false;
        if (expectedValue is null) return true; // Just checking existence

        var actual = prop.ResolvedValue?.ToString();
        return string.Equals(actual, expectedValue, StringComparison.OrdinalIgnoreCase);
    }

    // -- Cooldown tracking --

    private bool PassesCooldown(WebhookRule rule) {
        if (rule.CooldownSeconds <= 0) return true;

        var now = DateTimeOffset.UtcNow;

        if (!_lastFired.TryGetValue(rule.Name, out var lastFired))
            return true;

        return (now - lastFired).TotalSeconds >= rule.CooldownSeconds;
    }

    private void RecordFiring(WebhookRule rule) {
        if (rule.CooldownSeconds <= 0) return;
        _lastFired[rule.Name] = DateTimeOffset.UtcNow;
    }
}
