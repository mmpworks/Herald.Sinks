#nullable enable

using System.Collections.Generic;
using System.Threading;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Pipeline.Kernel;
using MMP.Herald.Templating;

namespace MMP.Herald.Tests.Helpers;

/// <summary>
/// Test sink that records every event it receives, whether via the
/// ILogger chain path or the kernel fast path. Lets parity tests assert
/// that both paths produce equivalent observable data for the same input.
/// </summary>
public sealed class SpyKernelSink : ILogger, IKernelSink
{
    private readonly List<RecordedEvent> _events = new();
    private readonly object _sync = new();

    public IReadOnlyList<RecordedEvent> Events
    {
        get { lock (_sync) return new List<RecordedEvent>(_events); }
    }

    public int Count
    {
        get { lock (_sync) return _events.Count; }
    }

    public bool Drove_KernelPath
    {
        get { lock (_sync) foreach (var e in _events) if (e.ViaKernel) return true; return false; }
    }

    public bool Drove_ChainPath
    {
        get { lock (_sync) foreach (var e in _events) if (!e.ViaKernel) return true; return false; }
    }

    public void Clear()
    {
        lock (_sync) _events.Clear();
    }

    public void Log(LogEvent logEvent)
    {
        lock (_sync)
        {
            _events.Add(new RecordedEvent(
                Level: logEvent.Level.Key,
                Category: logEvent.Category.Value,
                MessageTemplate: logEvent.MessageTemplate,
                PropertyNames: ExtractNames(logEvent.Properties),
                PropertyCount: logEvent.Properties.Count,
                ViaKernel: false));
        }
    }

    public void Log(in LogEventBuffer buffer)
    {
        var names = new List<string>(buffer.Properties.Length);
        for (var i = 0; i < buffer.Properties.Length; i++)
        {
            names.Add(buffer.Properties[i].Name);
        }

        lock (_sync)
        {
            _events.Add(new RecordedEvent(
                Level: buffer.Level.Key,
                Category: buffer.Category.Value,
                MessageTemplate: buffer.MessageTemplate,
                PropertyNames: names,
                PropertyCount: buffer.Properties.Length,
                ViaKernel: true));
        }
    }

    private static IReadOnlyList<string> ExtractNames(IReadOnlyList<LogProperty> properties)
    {
        var names = new List<string>(properties.Count);
        foreach (var p in properties) names.Add(p.Name);
        return names;
    }

    public sealed record RecordedEvent(
        string Level,
        string Category,
        string MessageTemplate,
        IReadOnlyList<string> PropertyNames,
        int PropertyCount,
        bool ViaKernel);
}
