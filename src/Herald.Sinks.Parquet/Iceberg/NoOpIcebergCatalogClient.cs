// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace Herald.Sinks.Parquet.Iceberg;

/// <summary>
/// Default <see cref="IIcebergCatalogClient"/> that drops every commit on
/// the floor. The Parquet sink uses this when no catalog client is
/// supplied; consumers wiring up an actual Iceberg REST catalog inject a
/// real implementation.
/// </summary>
public sealed class NoOpIcebergCatalogClient : IIcebergCatalogClient
{
    public static readonly NoOpIcebergCatalogClient Instance = new();

    public Task CommitFileAsync(string filePath, long rowCount, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
