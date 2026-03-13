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
    /// A list of raw <see cref="LeetCodeQuestionNodeDto"/> instances with <c>Content</c> populated.
    /// </returns>
    /// <exception cref="System.Net.Http.HttpRequestException">
    /// Thrown when the GraphQL response contains <c>errors</c>, or when
    /// <c>problemsetQuestionList</c> / <c>questions</c> is missing on any page.
    /// </exception>
    Task<List<LeetCodeQuestionNodeDto>> FetchAllProblemsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the HTML content string for a single problem identified by its slug.
    /// </summary>
    /// <param name="titleSlug">The URL-friendly title slug of the problem.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The HTML content string, or <c>null</c> if it could not be retrieved.</returns>
    Task<string?> FetchProblemContentAsync(string titleSlug, CancellationToken cancellationToken = default);
}
