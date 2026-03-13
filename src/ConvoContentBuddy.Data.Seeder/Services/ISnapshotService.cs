using ConvoContentBuddy.Data.Seeder.Models;

namespace ConvoContentBuddy.Data.Seeder.Services;

/// <summary>
/// Interface for snapshot persistence and retrieval operations, extracted to enable
/// unit testing of <see cref="ResilientLeetCodeDataSource"/> without real database calls.
/// </summary>
public interface ISnapshotService
{
    /// <summary>
    /// Serializes the raw catalog snapshot (preserving the full GraphQL envelope structure
    /// including total count and all enriched question nodes with <c>Content</c>) and persists
    /// it as the latest snapshot for the <c>leetcode-graphql</c> source.
    /// </summary>
    /// <param name="rawCatalog">The raw catalog envelope to snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PersistSnapshotAsync(LeetCodeRawCatalogSnapshotDto rawCatalog, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads and deserializes the most recent <c>leetcode-graphql</c> snapshot as a raw catalog envelope.
    /// Callers are responsible for mapping the envelope's questions to domain DTOs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The deserialized raw catalog envelope, or <c>null</c> if no snapshot exists.
    /// </returns>
    Task<LeetCodeRawCatalogSnapshotDto?> LoadLatestSnapshotAsync(CancellationToken cancellationToken = default);
}
