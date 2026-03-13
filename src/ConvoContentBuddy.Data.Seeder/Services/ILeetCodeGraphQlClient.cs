using ConvoContentBuddy.Data.Seeder.Models;

namespace ConvoContentBuddy.Data.Seeder.Services;

/// <summary>
/// Interface for the raw LeetCode GraphQL HTTP transport, extracted to enable
/// unit testing of <see cref="ResilientLeetCodeDataSource"/> without real HTTP calls.
/// </summary>
public interface ILeetCodeGraphQlClient
{
    /// <summary>
    /// Fetches all problems from the LeetCode GraphQL catalog endpoint by paginating
    /// through the full result set, then enriches each item with its per-problem content
    /// fetched from the detail query.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The full <see cref="LeetCodeCatalogResponseDto"/> GraphQL envelope with all paginated
    /// questions aggregated under <c>data.problemsetQuestionList</c> and <c>Content</c>
    /// populated on every node from the per-problem detail query.
    /// </returns>
    /// <exception cref="System.Net.Http.HttpRequestException">
    /// Thrown when the GraphQL response contains <c>errors</c>, when
    /// <c>problemsetQuestionList</c> / <c>questions</c> is missing on any page, or when
    /// any per-problem detail fetch fails (GraphQL errors, null content, or exhausted retries).
    /// </exception>
    Task<LeetCodeCatalogResponseDto> FetchAllProblemsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the HTML content string for a single problem identified by its slug.
    /// </summary>
    /// <param name="titleSlug">The URL-friendly title slug of the problem.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The HTML content string. Never returns <c>null</c>.</returns>
    /// <exception cref="System.Net.Http.HttpRequestException">
    /// Thrown when the detail query returns GraphQL <c>errors</c>, when the <c>content</c>
    /// field is <c>null</c> or missing, or when the request fails after all retry attempts.
    /// </exception>
    Task<string> FetchProblemContentAsync(string titleSlug, CancellationToken cancellationToken = default);
}
