// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

namespace Herald.Sinks.PagerDuty;

/// <summary>
/// Picks how <see cref="PagerDutyLogSink"/> derives the
/// <c>dedup_key</c> on outgoing events. The PagerDuty Events API
/// folds incidents that share a dedup_key into one open alert, so
/// the strategy directly controls how aggressively repeated events
/// collapse.
/// </summary>
public enum PagerDutyDedupStrategy
{
    /// <summary>
    /// The original fallback chain: event-id when set, otherwise the
    /// message template, otherwise a hash of the message. Matches the
    /// pre-strategy behaviour, so deployments that don't pick a
    /// strategy stay on this shape.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Use the event's id when present, otherwise the message
    /// template. Pin this for pipelines that assign a stable id per
    /// logical event and want guaranteed per-id dedup.
    /// </summary>
    EventId = 1,

    /// <summary>
    /// Use a hash of the message template only. Every event that
    /// shares the same template folds — useful when message text
    /// includes per-event interpolation but the alert family is the
    /// template.
    /// </summary>
    Template = 2,

    /// <summary>
    /// Use the event's category. One open incident per category —
    /// the right shape for sinks alerting on coarse failure
    /// classes (e.g. "billing", "auth").
    /// </summary>
    Category = 3,

    /// <summary>
    /// Use a hash of the rendered message. Per-distinct-message
    /// incidents — useful when each unique error string deserves
    /// its own page.
    /// </summary>
    Message = 4,
}
