using ConvoContentBuddy.Data.Seeder.Models;

namespace ConvoContentBuddy.Data.Seeder.Services;

/// <summary>
/// Abstraction over the LeetCode data ingestion pipeline.
/// Implementations handle live vs. snapshot fallback internally.
/// </summary>
public interface ILeetCodeDataSource
{
    /// <summary>
    /// Fetches the full catalog of LeetCode problems, potentially falling back to a
    /// cached snapshot if the live data source is unavailable.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of <see cref="LeetCodeProblemDto"/> instances.</returns>
    Task<IReadOnlyList<LeetCodeProblemDto>> FetchCatalogAsync(CancellationToken cancellationToken = default);
}
