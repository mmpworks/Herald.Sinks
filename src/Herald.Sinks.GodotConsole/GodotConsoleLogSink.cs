// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Globalization;
using System.Text;
using Godot;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Services;
// `using Godot;` brings Godot.Environment into scope and shadows
// System.Environment. Alias the BCL type so .NewLine resolves
// unambiguously to System.Environment.
using SysEnv = System.Environment;

namespace Herald.Sinks.GodotConsole;

/// <summary>
/// Sink that routes Herald log events to Godot 4.x's built-in
/// console output channels.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Below Warn — <see cref="GD.Print(string)"/>. Appears in the
///     Output panel with the default style.</item>
///   <item>Warn — <see cref="GD.PushWarning(string)"/>. Appears in the
///     Warnings tab and in the editor's debug overlay if enabled.</item>
///   <item>Error and above — <see cref="GD.PushError(string)"/>. Appears
///     in the Errors tab and produces an audible stinger in the editor.</item>
/// </list>
/// <para>
/// Format: <c>[HH:mm:ss.fff] [level] [category] message</c>, with
/// exception text appended after a newline when the event carries one.
/// Godot's Output panel is line-oriented and does not render ANSI
/// color codes, so the format stays text-only.
/// </para>
/// <para>
/// The sink dispatches via three injectable callbacks (default:
/// <see cref="GD.Print(string)"/> / <see cref="GD.PushWarning(string)"/>
/// / <see cref="GD.PushError(string)"/>). Tests substitute mocks via
/// the internal constructor; production uses the parameterless ctor
/// that wires the real Godot APIs.
/// </para>
/// </remarks>
public sealed class GodotConsoleLogSink : HeraldSinkBase
{
    private readonly Action<string> _print;
    private readonly Action<string> _warn;
    private readonly Action<string> _error;
    private readonly string? _category;

    /// <summary>
    /// Create a sink that routes to Godot's built-in console
    /// channels.
    /// </summary>
    /// <param name="category">
    /// Optional category prefix prepended to every emitted line.
    /// Useful for filtering Herald output from other engine chatter.
    /// </param>
    public GodotConsoleLogSink(string? category = null)
        : this(GD.Print, GD.PushWarning, GD.PushError, category)
    {
    }

    // Internal ctor used by tests to inject mock dispatch delegates.
    // Keeps the public surface simple while letting unit tests verify
    // routing without standing up a Godot runtime.
    internal GodotConsoleLogSink(
        Action<string> print,
        Action<string> warn,
        Action<string> error,
        string? category = null)
    {
        ArgumentNullException.ThrowIfNull(print);
        ArgumentNullException.ThrowIfNull(warn);
        ArgumentNullException.ThrowIfNull(error);

        _print = print;
        _warn = warn;
        _error = error;
        _category = category;
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        var line = FormatLine(logEvent);
        var dispatch = SelectDispatch(logEvent.Level.Key);
        dispatch(line);
    }

    private Action<string> SelectDispatch(string levelKey) => levelKey switch
    {
        LogLevelKeys.Error or LogLevelKeys.Critical or LogLevelKeys.Security => _error,
        LogLevelKeys.Warn                                                     => _warn,
        _                                                                     => _print,
    };

    private string FormatLine(LogEvent evt)
    {
        var time = evt.TimeUtc.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var level = evt.Level.Key;
        var category = evt.Category.Value;
        var message = string.IsNullOrEmpty(evt.Message) ? evt.MessageTemplate : evt.Message;

        var sb = new StringBuilder(64 + message.Length);
        if (!string.IsNullOrEmpty(_category))
        {
            sb.Append('[').Append(_category).Append("] ");
        }
        sb.Append('[').Append(time).Append("] [").Append(level).Append("] [")
          .Append(category).Append("] ").Append(message);

        if (evt.Context.TryGetValue(LogContextKeys.Exception, out var value)
            && value is Exception ex)
        {
            sb.Append(SysEnv.NewLine).Append(ex);
        }

        return sb.ToString();
    }
}
