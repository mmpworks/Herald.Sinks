// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Globalization;
using System.Text;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Services;

namespace Herald.Sinks.UnityConsole;

/// <summary>
/// Sink that routes Herald log events to Unity's console via
/// caller-supplied dispatch delegates.
/// </summary>
/// <remarks>
/// <para>
/// The sink takes three <see cref="Action{String}"/> delegates at
/// construction. Consumers wire them to Unity's API:
/// </para>
/// <code>
/// var sink = new UnityConsoleLogSink(
///     log:   UnityEngine.Debug.Log,
///     warn:  UnityEngine.Debug.LogWarning,
///     error: UnityEngine.Debug.LogError);
/// </code>
/// <para>
/// Routing:
/// <list type="bullet">
///   <item>Below Warn — <c>log</c> delegate (Unity's <c>Debug.Log</c>).</item>
///   <item>Warn — <c>warn</c> delegate (Unity's <c>Debug.LogWarning</c>).</item>
///   <item>Error and above — <c>error</c> delegate (Unity's <c>Debug.LogError</c>).</item>
/// </list>
/// </para>
/// <para>
/// The package has zero compile-time dependency on UnityEngine — it
/// references only the BCL and Herald.Core. Result: the sink ships
/// as a pure NuGet package, IL2CPP and Native AOT publishes are
/// safe, and tests can substitute simple <see cref="Action{String}"/>
/// mocks. The Unity-specific wiring lives in the consumer's
/// Unity-aware code.
/// </para>
/// </remarks>
public sealed class UnityConsoleLogSink : HeraldSinkBase
{
    private readonly Action<string> _log;
    private readonly Action<string> _warn;
    private readonly Action<string> _error;
    private readonly string? _category;

    /// <summary>
    /// Create a sink that routes Herald levels to caller-supplied
    /// Unity-shaped dispatch delegates.
    /// </summary>
    /// <param name="log">Dispatch for trace / debug / info / below-warn levels (typically <c>UnityEngine.Debug.Log</c>).</param>
    /// <param name="warn">Dispatch for warn-level events (typically <c>UnityEngine.Debug.LogWarning</c>).</param>
    /// <param name="error">Dispatch for error / critical / fatal levels (typically <c>UnityEngine.Debug.LogError</c>).</param>
    /// <param name="category">Optional category prefix prepended to every emitted line.</param>
    public UnityConsoleLogSink(
        Action<string> log,
        Action<string> warn,
        Action<string> error,
        string? category = null)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(warn);
        ArgumentNullException.ThrowIfNull(error);

        _log = log;
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
        LogLevelKeys.Error or LogLevelKeys.Fatal or LogLevelKeys.Security => _error,
        LogLevelKeys.Warning                                                     => _warn,
        _                                                                     => _log,
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
            sb.Append(Environment.NewLine).Append(ex);
        }

        return sb.ToString();
    }
}
