using ConvoContentBuddy.Data.Entities;

namespace ConvoContentBuddy.Data.Repositories;

/// <summary>
/// Repository interface for <see cref="IngestionSnapshot"/> persistence operations.
/// </summary>
public interface ISnapshotRepository
{
    /// <summary>
    /// Persists a new <see cref="IngestionSnapshot"/> to the store.
    /// The snapshot's <see cref="IngestionSnapshot.Source"/> determines which source-scoped
    /// latest marker is affected when <see cref="MarkAsLatestAsync"/> is subsequently called.
    /// </summary>
    /// <param name="snapshot">The snapshot to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PersistSnapshotAsync(IngestionSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent snapshot for the specified <paramref name="source"/> where
    /// <see cref="IngestionSnapshot.IsLatest"/> is <c>true</c>, or <c>null</c> if none exists
    /// for that source.  Scoping by source prevents ambiguity when multiple ingestion sources
    /// have each written their own <c>is_latest=true</c> row.
    /// </summary>
    /// <param name="source">
    /// The data-source identifier (matches <see cref="IngestionSnapshot.Source"/>) whose latest
    /// snapshot should be returned.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IngestionSnapshot?> LoadLatestAsync(string source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets <see cref="IngestionSnapshot.IsLatest"/> to <c>true</c> for the specified snapshot
    /// and <c>false</c> for all other snapshots sharing the same <see cref="IngestionSnapshot.Source"/>.
    /// The scope of the update is always limited to the source recorded on the target snapshot,
    /// so snapshots belonging to other sources are never affected.
    /// </summary>
    /// <param name="snapshotId">The ID of the snapshot to mark as latest.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkAsLatestAsync(Guid snapshotId, CancellationToken cancellationToken = default);
}
