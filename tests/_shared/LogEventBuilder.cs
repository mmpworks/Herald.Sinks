#nullable enable

using System;
using System.Collections.Generic;
using MMP.Herald.Events;
using MMP.Herald.Levels;
using MMP.Herald.Templating;

namespace MMP.Herald.Tests.Helpers;

/// <summary>
/// Fluent builder for creating LogEvent instances in tests.
/// </summary>
public sealed class LogEventBuilder
{
    private DateTimeOffset _time = new(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private LogLevel _level = KnownLogLevels.Info;
    private LogCategory _category = LogCategory.App;
    private string _messageTemplate = "Test message";
    private string _message = "Test message";
    private IReadOnlyList<LogProperty> _properties = LogEvent.EmptyProperties;
    private IReadOnlyDictionary<string, object?> _context = LogEvent.EmptyContext;
    private LogEventId? _eventId;
    private string? _causedBy;

    public static LogEventBuilder Create() => new();

    public LogEventBuilder WithLevel(LogLevel level) { _level = level; return this; }
    public LogEventBuilder WithCategory(LogCategory category) { _category = category; return this; }
    public LogEventBuilder WithMessage(string template, string? rendered = null)
    {
        _messageTemplate = template;
        _message = rendered ?? template;
        return this;
    }
    public LogEventBuilder WithTime(DateTimeOffset time) { _time = time; return this; }
    public LogEventBuilder WithEventId(int id, string? name = null) { _eventId = new LogEventId(id, name); return this; }
    public LogEventBuilder WithCausedBy(string causedBy) { _causedBy = causedBy; return this; }

    public LogEventBuilder WithProperty(string name, object? value,
        LogPropertyCaptureMode? captureMode = null, string? format = null)
    {
        var list = new List<LogProperty>(_properties) { new(name, value, captureMode, format) };
        _properties = list;
        return this;
    }

    public LogEventBuilder WithContext(string key, object? value)
    {
        var dict = new Dictionary<string, object?>(_context) { [key] = value };
        _context = dict;
        return this;
    }

    public LogEvent Build() => new(
        _time, _level, _category, _messageTemplate, _message,
        _properties, _context, _eventId, _causedBy);
}
