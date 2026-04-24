// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System.Collections.Generic;

namespace Herald.Sinks.GenericWebhook;

/// <summary>
/// A single webhook alerting rule that defines when and how to fire a webhook.
///
/// Inspired by real-world alerting systems:
///   - PagerDuty: event rules with conditions (severity, source, summary regex)
///   - Opsgenie: alert policies with filters (message, tags, priority)
///   - Datadog: monitor-based alerting with threshold conditions
///   - Grafana Alerting: multi-dimensional rules with label matchers
///
/// Each rule has a set of conditions (all must match - AND logic) and an optional
/// cooldown to prevent flooding. Rules are evaluated in order; the first matching
/// rule wins unless ContinueOnMatch is true.
/// </summary>
public sealed record WebhookRule
{
    /// <summary>Unique name for this rule. Used in logging and dashboard display.</summary>
    public required string Name { get; init; }

    /// <summary>All conditions must match for the rule to fire (AND logic).</summary>
    public required IReadOnlyList<WebhookRuleCondition> Conditions { get; init; }

    /// <summary>
    /// Minimum seconds between consecutive firings of this rule.
    /// Prevents webhook flooding for high-frequency matching events.
    /// Set to 0 for no cooldown (fire on every match).
    ///
    /// Similar to PagerDuty's dedup_key suppression and Opsgenie's alert count threshold.
    /// </summary>
    public int CooldownSeconds { get; init; }

    /// <summary>
    /// When true, evaluation continues to the next rule after this one matches.
    /// When false (default), the first matching rule wins and no further rules are checked.
    ///
    /// Similar to Grafana's "continue matching" option in notification policies.
    /// </summary>
    public bool ContinueOnMatch { get; init; }

    /// <summary>
    /// Optional custom payload template. When set, this JSON template is sent instead
    /// of the default formatter output. Supports placeholders:
    ///   {level}, {category}, {message}, {timestamp}, {ruleName},
    ///   {prop:PropertyName} for structured properties.
    ///
    /// Similar to PagerDuty's custom event transformer and Opsgenie's action mappings.
    /// </summary>
    public string? PayloadTemplate { get; init; }

    /// <summary>
    /// Optional severity override sent with the webhook payload.
    /// Maps to PagerDuty severity, Opsgenie priority, Datadog alert type.
    /// Values: "critical", "error", "warning", "info".
    /// Null means derive from the log event's level.
    /// </summary>
    public string? SeverityOverride { get; init; }

    // -- Factory methods for common patterns --

    /// <summary>Create a rule matching events at or above a level threshold.</summary>
    public static WebhookRule ForLevel(string name, string minLevel, int cooldownSeconds = 60) =>
        new()
        {
            Name = name,
            Conditions = [WebhookRuleCondition.MinLevel(minLevel)],
            CooldownSeconds = cooldownSeconds
        };

    /// <summary>Create a rule matching a specific category with level threshold.</summary>
    public static WebhookRule ForCategory(string name, string category, string minLevel = "error", int cooldownSeconds = 60) =>
        new()
        {
            Name = name,
            Conditions = [WebhookRuleCondition.MinLevel(minLevel), WebhookRuleCondition.CategoryEquals(category)],
            CooldownSeconds = cooldownSeconds
        };

    /// <summary>Create a rule matching a message regex pattern.</summary>
    public static WebhookRule ForPattern(string name, string messagePattern, int cooldownSeconds = 30) =>
        new()
        {
            Name = name,
            Conditions = [WebhookRuleCondition.MessageMatches(messagePattern)],
            CooldownSeconds = cooldownSeconds
        };
}
