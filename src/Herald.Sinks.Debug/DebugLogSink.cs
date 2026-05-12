// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Globalization;
using MMP.Herald;
using MMP.Herald.Events;
using MMP.Herald.Services;
// The namespace `Herald.Sinks.Debug` shadows `System.Diagnostics.Debug`,
// so we alias the BCL type to a non-conflicting name instead of using
// `using System.Diagnostics;` and writing `Debug.WriteLine` ambiguously.
using SysDebug = System.Diagnostics.Debug;

namespace Herald.Sinks.Debug;

/// <summary>
/// Sink that writes log events to <see cref="System.Diagnostics.Debug"/>.
/// In Visual Studio / JetBrains Rider / VS Code with a debugger attached,
/// events land in the Output window. Under <c>dotnet test</c> or
/// <c>dotnet run</c> without a debugger, the output is still visible when
/// a <see cref="DebugListener"/> is registered on
/// <see cref="Debug.Listeners"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this package defines DEBUG.</b> <see cref="Debug.WriteLine(string)"/>
/// is decorated with <c>[Conditional("DEBUG")]</c>. The compiler checks the
/// DEFINING assembly (this one) for the DEBUG constant — if it is absent,
/// the call is stripped from this assembly's IL. The csproj forces DEBUG
/// to always be defined so the WriteLine survives the Release build of
/// this NuGet package. Downstream consumers do not need to do anything
/// special.
/// </para>
/// <para>
/// Format: <c>[HH:mm:ss.fff] [level] [category] message</c>. Exceptions
/// are appended as <c>\n{stack}</c>. Minimal by design — pair with a
/// custom transformer chain in the pipeline if a different layout is
/// needed.
/// </para>
/// </remarks>
public sealed class DebugLogSink : ILogger
{
    private readonly string? _category;

    /// <summary>
    /// Create a Debug sink. The <paramref name="category"/> argument is
    /// forwarded to <see cref="Debug.WriteLine(string, string)"/> so
    /// listeners that group by category (Visual Studio's Output window
    /// does) can filter Herald output from other diagnostic chatter.
    /// </summary>
    public DebugLogSink(string? category = null)
    {
        _category = category;
    }

    public void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        var line = FormatLine(logEvent);

        if (_category is null)
            SysDebug.WriteLine(line);
        else
            SysDebug.WriteLine(line, _category);
    }

    private static string FormatLine(LogEvent evt)
    {
        var time = evt.TimeUtc.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var level = evt.Level.Key;
        var category = evt.Category.Value;
        var message = string.IsNullOrEmpty(evt.Message) ? evt.MessageTemplate : evt.Message;

        var line = $"[{time}] [{level}] [{category}] {message}";

        if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value) && value is Exception ex)
        {
            line += Environment.NewLine + ex;
        }

        return line;
    }
}
