// Copyright (c) 2026 MMP LLC
// Licensed under the MIT License. See LICENSE in the project root.
#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace Herald.Sinks.Parquet.Iceberg;

/// <summary>
/// Seam for committing Parquet files into an Iceberg catalog snapshot
/// after the Parquet sink closes them. The default implementation
/// (<see cref="NoOpIcebergCatalogClient"/>) does nothing — wire up a real
/// REST-catalog-backed implementation when the catalog endpoint is
/// available.
/// </summary>
/// <remarks>
/// Iceberg is a table format, not a sink. The Parquet sink writes the
/// data files; an external catalog (PyIceberg, Java Iceberg, or a custom
/// REST client) records snapshot metadata. This interface lets the sink
/// notify a catalog without taking on the Iceberg spec itself.
/// </remarks>
public interface IIcebergCatalogClient
{
    /// <summary>
    /// Notify the catalog that a new Parquet data file has been written
    /// and should be added as a new snapshot of the configured table.
    /// </summary>
    /// <param name="filePath">Absolute path or URI to the closed Parquet file.</param>
    /// <param name="rowCount">Number of rows the file contains.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CommitFileAsync(string filePath, long rowCount, CancellationToken cancellationToken = default);
}
