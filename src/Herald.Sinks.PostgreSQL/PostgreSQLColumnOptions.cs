// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

namespace Herald.Sinks.PostgreSQL;

/// <summary>
/// Column names used by <see cref="PostgreSQLLogSink"/>. Override
/// individual names to align with an existing schema; the sink writes
/// every column regardless of what it is called, as long as the type
/// matches the shape documented in the remarks.
/// </summary>
/// <remarks>
/// Default column types expected by the sink (PostgreSQL):
/// <list type="bullet">
///   <item><c>id</c> — BIGSERIAL PRIMARY KEY (assigned by the DB; sink does not write it).</item>
///   <item><c>time_utc</c> — TIMESTAMPTZ NOT NULL.</item>
///   <item><c>level</c> — TEXT NOT NULL.</item>
///   <item><c>category</c> — TEXT NOT NULL.</item>
///   <item><c>message</c> — TEXT NOT NULL.</item>
///   <item><c>template</c> — TEXT NOT NULL.</item>
///   <item><c>exception</c> — TEXT NULL.</item>
///   <item><c>properties</c> — JSONB NULL.</item>
/// </list>
/// PostgreSQL's case-folding makes unquoted identifiers lowercase;
/// defaults use snake_case to keep tables readable under
/// <c>SELECT * FROM logs</c> without quoting.
/// </remarks>
public sealed class PostgreSQLColumnOptions
{
    public string Id { get; init; } = "id";
    public string TimeUtc { get; init; } = "time_utc";
    public string Level { get; init; } = "level";
    public string Category { get; init; } = "category";
    public string Message { get; init; } = "message";
    public string Template { get; init; } = "template";
    public string Exception { get; init; } = "exception";
    public string Properties { get; init; } = "properties";

    public static PostgreSQLColumnOptions Default { get; } = new();
}
