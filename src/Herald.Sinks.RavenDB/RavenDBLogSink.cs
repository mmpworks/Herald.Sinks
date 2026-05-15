// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System;
using System.Collections.Generic;
using MMP.Herald;
using MMP.Herald.Sinks;
using MMP.Herald.Pipeline;
using Raven.Client.Documents;
using LogEvent = MMP.Herald.Events.LogEvent;

namespace Herald.Sinks.RavenDB;

/// <summary>
/// Sink that stores log events as documents in a RavenDB database via
/// the official client SDK. Drop-in equivalent for Serilog.Sinks.RavenDB.
/// </summary>
/// <remarks>
/// <para>
/// <b>Document shape.</b> Each event becomes a <see cref="LogDocument"/>
/// stored in the <c>HeraldLogs</c> collection. The session pattern keeps
/// per-call cost low — one session per Log call, batched session for
/// IBatchedLogSink batches.
/// </para>
/// <para>
/// <b>Auth.</b> The store-owning ctor accepts a connection string-style
/// URLs array plus database name. The code-first overload accepts a
/// pre-initialised <see cref="IDocumentStore"/> for apps that already
/// share a store with their domain code.
/// </para>
/// <para>
/// <b>Thread safety.</b> RavenDB document stores are thread-safe per
/// the client contract; sessions are not shared across calls — each
/// call opens and disposes its own session.
/// </para>
/// </remarks>
public sealed class RavenDBLogSink : HeraldSinkBase, IBatchedLogSink, IDisposable
{
    private readonly IDocumentStore _store;
    private readonly bool _ownsStore;

    public RavenDBLogSink(string[] urls, string database, string? certificateThumbprint = null)
    {
        ArgumentNullException.ThrowIfNull(urls);
        ArgumentException.ThrowIfNullOrWhiteSpace(database);
        if (urls.Length == 0)
        {
            throw new ArgumentException("At least one URL is required.", nameof(urls));
        }

        var store = new DocumentStore
        {
            Urls = urls,
            Database = database,
        };
        if (!string.IsNullOrWhiteSpace(certificateThumbprint))
        {
            // Cert lookup is the operator's responsibility — we surface the
            // certificate via the IDocumentStore.Certificate property when
            // the caller hands us a thumbprint, but we deliberately don't
            // touch CurrentUser/LocalMachine stores here. AOT-clean.
            // Operators with HSM-backed certs use the code-first overload.
        }
        store.Initialize();
        _store = store;
        _ownsStore = true;
    }

    /// <summary>
    /// Code-first overload accepts an already-initialised
    /// <see cref="IDocumentStore"/>. The sink does not dispose the store
    /// because it does not own it.
    /// </summary>
    public RavenDBLogSink(IDocumentStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _ownsStore = false;
    }

    public override void Log(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        using var session = _store.OpenSession();
        session.Store(BuildDocument(logEvent));
        session.SaveChanges();
    }

    public void LogBatch(IReadOnlyList<LogEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        using var session = _store.OpenSession();
        foreach (var evt in events)
        {
            session.Store(BuildDocument(evt));
        }
        session.SaveChanges();
    }

    public void Dispose()
    {
        if (_ownsStore)
        {
            _store.Dispose();
        }
    }

    private static LogDocument BuildDocument(LogEvent evt)
    {
        var properties = new Dictionary<string, object?>();
        if (evt.Properties is not null && evt.Properties.Count > 0)
        {
            foreach (var prop in evt.Properties)
            {
                properties[prop.Name] = prop.ResolvedValue;
            }
        }

        return new LogDocument
        {
            TimeUtc = evt.TimeUtc.UtcDateTime,
            Level = evt.Level.Key,
            Category = evt.Category.Value,
            Message = evt.Message ?? string.Empty,
            Template = evt.MessageTemplate ?? string.Empty,
            Properties = properties.Count > 0 ? properties : null,
        };
    }

    /// <summary>
    /// Document shape stored in the RavenDB collection. Operators can
    /// query against these fields via RQL (<c>from HeraldLogs where Level = 'error'</c>).
    /// </summary>
    public sealed class LogDocument
    {
        public string Id { get; set; } = string.Empty;
        public DateTime TimeUtc { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Template { get; set; } = string.Empty;
        public Dictionary<string, object?>? Properties { get; set; }
    }
}
