using ConvoContentBuddy.Data.Seeder.Models;

namespace ConvoContentBuddy.Data.Seeder.Services;

/// <summary>
/// Interface for snapshot persistence and retrieval operations, extracted to enable
/// unit testing of <see cref="ResilientLeetCodeDataSource"/> without real database calls.
/// </summary>
public interface ISnapshotService
{
    /// <summary>
    /// Persists the raw GraphQL response capture directly to the snapshot store, preserving
    /// every field returned by the live LeetCode endpoint regardless of whether it is
    /// currently modeled in a DTO. The <see cref="LeetCodeRawCaptureDto.MappedNodes"/>
    /// runtime-only list is intentionally excluded from the persisted payload.
    /// </summary>
    /// <param name="rawCapture">The raw response capture produced by the live fetch path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PersistSnapshotAsync(LeetCodeRawCaptureDto rawCapture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the most recent <c>leetcode-graphql</c> snapshot and deserializes it as a
    /// <see cref="LeetCodeRawCaptureDto"/>. Callers are responsible for mapping the raw
    /// catalog pages and detail responses to domain DTOs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The deserialized raw capture, or <c>null</c> if no snapshot exists.
    /// Note: <see cref="LeetCodeRawCaptureDto.MappedNodes"/> will be empty on replay —
    /// callers must reconstruct problem data from
    /// <see cref="LeetCodeRawCaptureDto.RawCatalogPages"/> and
    /// <see cref="LeetCodeRawCaptureDto.RawDetailResponses"/>.
    /// </returns>
    Task<LeetCodeRawCaptureDto?> LoadLatestSnapshotAsync(CancellationToken cancellationToken = default);
}
