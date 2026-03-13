using ConvoContentBuddy.Data.Seeder.Models;

namespace ConvoContentBuddy.Data.Seeder.Services;

/// <summary>
/// Interface for snapshot persistence and retrieval operations, extracted to enable
/// unit testing of <see cref="ResilientLeetCodeDataSource"/> without real database calls.
/// </summary>
public interface ISnapshotService
{
    /// <summary>
    /// Serializes the raw catalog nodes (preserving the GraphQL response shape including
    /// <c>Content</c>) and persists them as the latest snapshot for the <c>leetcode-graphql</c> source.
    /// </summary>
    /// <param name="rawNodes">The raw catalog nodes to snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PersistSnapshotAsync(IReadOnlyList<LeetCodeQuestionNodeDto> rawNodes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads and deserializes the most recent <c>leetcode-graphql</c> snapshot as raw catalog nodes.
    /// Callers are responsible for mapping the nodes to domain DTOs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The deserialized list of raw catalog nodes, or <c>null</c> if no snapshot exists.
    /// </returns>
    Task<IReadOnlyList<LeetCodeQuestionNodeDto>?> LoadLatestSnapshotAsync(CancellationToken cancellationToken = default);
}
