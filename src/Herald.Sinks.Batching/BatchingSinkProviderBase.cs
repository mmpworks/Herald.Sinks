// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System.IO;
using MMP.Herald.Configuration.Runtime;
using MMP.Herald.Levels;
using MMP.Herald.Output.Rendering;
using MMP.Herald.Routing;

namespace MMP.Herald.Sinks.Batching;

/// <summary>
/// Base class for providers whose sinks ride the batching decorator. It
/// gives every batched sink the three batch knobs —
/// <c>batch_size</c>, <c>flush_interval_ms</c>, <c>queue_capacity</c> — on
/// its Dashboard configuration form without any per-mmpform edit.
///
/// <para>
/// <b>How the form injection works.</b> <see cref="ILogSinkProvider"/>
/// ships a default <c>GetFormSchemaText()</c> that reads the sink's own
/// <c>configuration-{SinkKind}.mmpform</c> (or <c>configuration.mmpform</c>)
/// embedded resource off the concrete provider's assembly. This base
/// re-implements that same resource lookup in <see cref="ReadOwnFormSchema"/>
/// — <c>GetType().Assembly</c> resolves to the concrete provider at
/// runtime, so the right form is found — then appends the shared batch
/// fields. The batch schema is a single source of truth in
/// <see cref="BatchFieldsSchema"/>; the property keys match
/// <see cref="BatchingOptions"/> so the form and the runtime read the same
/// names.
/// </para>
///
/// <para>
/// <b>Explicit interface implementation is deliberate.</b>
/// <c>GetFormSchemaText()</c> is implemented explicitly against
/// <see cref="ILogSinkProvider"/>, not as a <c>public virtual</c> member.
/// The Dashboard reaches every provider through the interface, so the
/// default-interface-method dispatch lands on this explicit implementation.
/// Concrete providers do not — and must not — override it; a public
/// override would shadow the interface dispatch and the batch fields would
/// silently drop off the form.
/// </para>
///
/// <para>
/// <c>GetCapabilityYaml()</c> is left to the interface default — the batch
/// decorator adds form fields, not a capability manifest — so a batched
/// sink's CAPABILITY.yaml still comes straight off its own assembly.
/// </para>
/// </summary>
public abstract class BatchingSinkProviderBase : ILogSinkProvider
{
    /// <summary>
    /// The lowercase sink kind this provider handles. Concrete providers
    /// override with their <c>KindKey</c>.
    /// </summary>
    public abstract string SinkKind { get; }

    /// <summary>
    /// Minimum Herald edition required to use this sink. Defaults to
    /// <see cref="HeraldEdition.Community"/>; a provider whose sink gates
    /// on a higher edition overrides this to surface that value.
    /// </summary>
    public virtual HeraldEdition MinimumEdition => HeraldEdition.Community;

    /// <summary>
    /// Creates a concrete sink from the given runtime definition. Matches
    /// the <see cref="ILogSinkProvider.CreateSink"/> contract; concrete
    /// providers override it and wrap their sink with
    /// <see cref="BatchingLogSinkDecorator.Wrap"/>.
    /// </summary>
    public abstract ILogger CreateSink(
        LoggingRuntimeSinkDefinition definition,
        ILogLevelRegistry levelRegistry,
        ILogOutputTransformerRegistry transformerRegistry);

    // Explicit interface implementation — DIM dispatch from the Dashboard
    // goes through ILogSinkProvider, so this is the form text every batched
    // sink returns. Append the batch fields to the sink's own form; when the
    // sink ships no form file, the batch fields stand alone.
    string? ILogSinkProvider.GetFormSchemaText()
    {
        var ownForm = ReadOwnFormSchema();
        return ownForm is null ? BatchFieldsSchema : ownForm + "\n" + BatchFieldsSchema;
    }

    // Re-implements the interface default's two-step resource lookup.
    // GetType().Assembly resolves to the concrete provider's assembly at
    // runtime, so a package that ships multiple per-kind forms still finds
    // the right one.
    private string? ReadOwnFormSchema()
    {
        var asm = GetType().Assembly;
        using var stream = asm.GetManifestResourceStream($"configuration-{SinkKind}.mmpform")
                        ?? asm.GetManifestResourceStream("configuration.mmpform");
        if (stream is null) return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // Single source of truth — appended to every batched sink's form. Field
    // names and defaults match BatchingOptions (batch_size 256,
    // flush_interval_ms 1000, queue_capacity 8192). The container is
    // self-contained so it renders as a distinct section below the sink's
    // own fields.
    private const string BatchFieldsSchema =
        """
        # Batching — shared knobs injected by BatchingSinkProviderBase.
        # The producer never blocks on delivery: events ride a bounded
        # channel and a background drain forwards them in size- or
        # time-bounded batches.

        columns: 12

        __properties = [
            "batch_size"        = { type: "integer", default: 256 },
            "flush_interval_ms" = { type: "integer", default: 1000 },
            "queue_capacity"    = { type: "integer", default: 8192 }
        ]

        tooltips = [
            "tt-batch-size"  = "Number of events per batch before flushing.",
            "tt-flush-ms"    = "Max time (ms) to hold a partial batch.",
            "tt-queue-cap"   = "Channel capacity before drop-on-overflow."
        ]

        [container("Batching", "Non-blocking delivery via a bounded channel and a background drain.")]

          - [number(12,{batch_size},tt="tt-batch-size",init=256)] Batch size
          - [number(12,{flush_interval_ms},tt="tt-flush-ms",init=1000)] Flush interval (ms)
          - [number(12,{queue_capacity},tt="tt-queue-cap",init=8192)] Queue capacity
        """;
}
