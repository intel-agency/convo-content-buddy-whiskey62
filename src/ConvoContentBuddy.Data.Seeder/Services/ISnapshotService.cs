using ConvoContentBuddy.Data.Seeder.Models;

namespace ConvoContentBuddy.Data.Seeder.Services;

/// <summary>
/// Interface for snapshot persistence and retrieval operations, extracted to enable
/// unit testing of <see cref="ResilientLeetCodeDataSource"/> without real database calls.
/// </summary>
public interface ISnapshotService
{
    /// <summary>
    /// Serializes the full GraphQL catalog response envelope (preserving the
    /// <c>data.problemsetQuestionList</c> wrapper, total count, enriched question nodes with
    /// <c>Content</c>, and any top-level <c>errors</c> field) and persists it as the latest
    /// snapshot for the <c>leetcode-graphql</c> source.
    /// </summary>
    /// <param name="catalogResponse">The full catalog GraphQL envelope to snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PersistSnapshotAsync(LeetCodeCatalogResponseDto catalogResponse, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads and deserializes the most recent <c>leetcode-graphql</c> snapshot as the full
    /// catalog GraphQL envelope. Callers are responsible for extracting
    /// <c>data.problemsetQuestionList.questions</c> and mapping nodes to domain DTOs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The deserialized catalog GraphQL envelope, or <c>null</c> if no snapshot exists.
    /// </returns>
    Task<LeetCodeCatalogResponseDto?> LoadLatestSnapshotAsync(CancellationToken cancellationToken = default);
}
