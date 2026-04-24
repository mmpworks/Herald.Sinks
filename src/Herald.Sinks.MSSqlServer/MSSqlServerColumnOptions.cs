// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

namespace Herald.Sinks.MSSqlServer;

/// <summary>
/// Column names used by <see cref="MSSqlServerLogSink"/>. Override
/// individual names to align with an existing schema; the sink writes
/// every column regardless of what it is called, as long as the type
/// matches the shape documented in each property's remarks.
/// </summary>
/// <remarks>
/// Default column types expected by the sink:
/// <list type="bullet">
///   <item><c>Id</c> — BIGINT IDENTITY(1,1) PRIMARY KEY (the sink does not
///   insert this; the DB assigns it).</item>
///   <item><c>TimeUtc</c> — DATETIME2(7) NOT NULL (UTC).</item>
///   <item><c>Level</c> — VARCHAR(32) NOT NULL (the level key, e.g. "info").</item>
///   <item><c>Category</c> — VARCHAR(128) NOT NULL.</item>
///   <item><c>Message</c> — NVARCHAR(MAX) NOT NULL.</item>
///   <item><c>Template</c> — NVARCHAR(MAX) NOT NULL.</item>
///   <item><c>Exception</c> — NVARCHAR(MAX) NULL (full exception text).</item>
///   <item><c>Properties</c> — NVARCHAR(MAX) NULL (JSON object).</item>
/// </list>
/// </remarks>
public sealed class MSSqlServerColumnOptions
{
    public string Id { get; init; } = "Id";
    public string TimeUtc { get; init; } = "TimeUtc";
    public string Level { get; init; } = "Level";
    public string Category { get; init; } = "Category";
    public string Message { get; init; } = "Message";
    public string Template { get; init; } = "Template";
    public string Exception { get; init; } = "Exception";
    public string Properties { get; init; } = "Properties";

    public static MSSqlServerColumnOptions Default { get; } = new();
}
