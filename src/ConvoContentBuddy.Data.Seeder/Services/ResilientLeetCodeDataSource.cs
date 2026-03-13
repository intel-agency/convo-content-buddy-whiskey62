using ConvoContentBuddy.Data.Seeder.Models;
using Microsoft.Extensions.Logging;

namespace ConvoContentBuddy.Data.Seeder.Services;

/// <summary>
/// Implements <see cref="ILeetCodeDataSource"/> with a live-first, snapshot-fallback strategy.
/// On success the raw catalog nodes (preserving the GraphQL shape including Content) are persisted
/// as a new snapshot, then mapped to <see cref="LeetCodeProblemDto"/> for the caller. On failure the
/// most recent snapshot is loaded and mapped. If neither is available, an <see cref="IngestionException"/> is thrown.
/// </summary>
public sealed class ResilientLeetCodeDataSource : ILeetCodeDataSource
{
    private readonly ILeetCodeGraphQlClient _graphQlClient;
    private readonly ISnapshotService _snapshotService;
    private readonly ILogger<ResilientLeetCodeDataSource> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ResilientLeetCodeDataSource"/>.
    /// </summary>
    /// <param name="graphQlClient">The live LeetCode GraphQL transport.</param>
    /// <param name="snapshotService">The snapshot persistence and retrieval service.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public ResilientLeetCodeDataSource(
        ILeetCodeGraphQlClient graphQlClient,
        ISnapshotService snapshotService,
        ILogger<ResilientLeetCodeDataSource> logger)
    {
        _graphQlClient = graphQlClient;
        _snapshotService = snapshotService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<LeetCodeProblemDto>> FetchCatalogAsync(CancellationToken cancellationToken = default)
    {
        Exception? liveException = null;

        try
        {
            _logger.LogInformation("Attempting to fetch LeetCode catalog via live GraphQL");
            var rawNodes = await _graphQlClient.FetchAllProblemsAsync(cancellationToken).ConfigureAwait(false);

            await _snapshotService.PersistSnapshotAsync(rawNodes, cancellationToken).ConfigureAwait(false);

            var problems = rawNodes.Select(MapToProblemDto).ToList();
            _logger.LogInformation("Successfully fetched {Count} problems from live GraphQL", problems.Count);
            return problems;
        }
        catch (Exception ex)
        {
            liveException = ex;
            _logger.LogWarning(ex,
                "Live LeetCode GraphQL fetch failed. Attempting snapshot fallback");
        }

        var cachedNodes = await _snapshotService.LoadLatestSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (cachedNodes is not null)
        {
            var cached = cachedNodes.Select(MapToProblemDto).ToList();
            _logger.LogInformation(
                "Falling back to cached snapshot containing {Count} problems", cached.Count);
            return cached;
        }

        throw new IngestionException(
            "No LeetCode data available — live GraphQL failed and no snapshot exists",
            liveException!);
    }

    private static LeetCodeProblemDto MapToProblemDto(LeetCodeQuestionNodeDto node)
    {
        _ = int.TryParse(node.QuestionFrontendId, out var questionId);
        return new LeetCodeProblemDto
        {
            TitleSlug = node.TitleSlug,
            QuestionId = questionId,
            Title = node.Title,
            Difficulty = node.Difficulty,
            TopicTags = node.TopicTags.Select(t => t.Name).ToList(),
            Content = node.Content
        };
    }
}
