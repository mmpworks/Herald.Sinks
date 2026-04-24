// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

namespace Herald.Sinks.GenericWebhook;

/// <summary>
/// A single condition within a webhook rule. Evaluated against each log event.
///
/// Condition types mirror real-world alerting platforms:
///   - Level threshold: PagerDuty severity, Datadog monitor status
///   - Category match: Opsgenie tags, Grafana labels
///   - Message pattern: PagerDuty summary regex, Elasticsearch watcher condition
///   - Property match: Datadog facets, Splunk field extraction
/// </summary>
public sealed record WebhookRuleCondition
{
    /// <summary>The type of comparison to perform.</summary>
    public required WebhookConditionType Type { get; init; }

    /// <summary>
    /// The value to compare against. Interpretation depends on Type:
    ///   MinLevel: level key string ("error", "critical")
    ///   CategoryEquals/CategoryContains: category string
    ///   MessageContains/MessageMatches: search string or regex pattern
    ///   PropertyEquals/PropertyExists: property name (use PropertyValue for the value)
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Secondary value for property-based conditions.
    /// For PropertyEquals: the expected property value.
    /// Ignored for other condition types.
    /// </summary>
    public string? PropertyValue { get; init; }

    // -- Factory methods for readable rule construction --

    /// <summary>Match events at or above a level threshold.</summary>
    public static WebhookRuleCondition MinLevel(string levelKey) =>
        new() { Type = WebhookConditionType.MinLevel, Value = levelKey };

    /// <summary>Match events with an exact category.</summary>
    public static WebhookRuleCondition CategoryEquals(string category) =>
        new() { Type = WebhookConditionType.CategoryEquals, Value = category };

    /// <summary>Match events whose category contains the given substring.</summary>
    public static WebhookRuleCondition CategoryContains(string substring) =>
        new() { Type = WebhookConditionType.CategoryContains, Value = substring };

    /// <summary>Match events whose message contains the given substring.</summary>
    public static WebhookRuleCondition MessageContains(string substring) =>
        new() { Type = WebhookConditionType.MessageContains, Value = substring };

    /// <summary>Match events whose message matches a regex pattern. Must use named capture groups.</summary>
    public static WebhookRuleCondition MessageMatches(string regexPattern) =>
        new() { Type = WebhookConditionType.MessageMatches, Value = regexPattern };

    /// <summary>Match events that have a property with the given name.</summary>
    public static WebhookRuleCondition PropertyExists(string propertyName) =>
        new() { Type = WebhookConditionType.PropertyExists, Value = propertyName };

    /// <summary>Match events where a property has a specific value.</summary>
    public static WebhookRuleCondition PropertyEquals(string propertyName, string propertyValue) =>
        new() { Type = WebhookConditionType.PropertyEquals, Value = propertyName, PropertyValue = propertyValue };
}

/// <summary>
/// Types of conditions that can be applied to webhook rules.
/// </summary>
public sealed record WebhookConditionType
{
    public string Key { get; }
    private WebhookConditionType(string key) => Key = key;

    public static readonly WebhookConditionType MinLevel = new("minLevel");
    public static readonly WebhookConditionType CategoryEquals = new("categoryEquals");
    public static readonly WebhookConditionType CategoryContains = new("categoryContains");
    public static readonly WebhookConditionType MessageContains = new("messageContains");
    public static readonly WebhookConditionType MessageMatches = new("messageMatches");
    public static readonly WebhookConditionType PropertyExists = new("propertyExists");
    public static readonly WebhookConditionType PropertyEquals = new("propertyEquals");
}
