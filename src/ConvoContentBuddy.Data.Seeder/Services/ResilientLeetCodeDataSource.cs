using System.Text.Json;
using ConvoContentBuddy.Data.Seeder.Models;
using Microsoft.Extensions.Logging;

namespace ConvoContentBuddy.Data.Seeder.Services;

/// <summary>
/// Implements <see cref="ILeetCodeDataSource"/> with a live-first, snapshot-fallback strategy.
/// On success the raw GraphQL response capture is persisted (preserving every field returned
/// by the live endpoint), then mapped to <see cref="LeetCodeProblemDto"/> for the caller. On
/// failure the most recent snapshot is loaded and its raw payloads are mapped to
/// <see cref="LeetCodeProblemDto"/> on replay. If neither is available, an
/// <see cref="IngestionException"/> is thrown.
/// </summary>
public sealed class ResilientLeetCodeDataSource : ILeetCodeDataSource
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
            var rawCapture = await _graphQlClient.FetchAllProblemsAsync(cancellationToken).ConfigureAwait(false);

            await _snapshotService.PersistSnapshotAsync(rawCapture, cancellationToken).ConfigureAwait(false);

            // On the live path MappedNodes is populated by the client, so use it directly
            // without re-parsing the raw JSON strings.
            var problems = rawCapture.MappedNodes.Select(MapToProblemDto).ToList();
            _logger.LogInformation("Successfully fetched {Count} problems from live GraphQL", problems.Count);
            return problems;
        }
        catch (Exception ex)
        {
            liveException = ex;
            _logger.LogWarning(ex,
                "Live LeetCode GraphQL fetch failed. Attempting snapshot fallback");
        }

        var cachedCapture = await _snapshotService.LoadLatestSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (cachedCapture is not null)
        {
            // On the replay path MappedNodes is empty (it is [JsonIgnore]), so reconstruct
            // problem data by parsing the preserved raw JSON strings.
            var cached = MapRawCaptureToProblems(cachedCapture);
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

    /// <summary>
    /// Reconstructs <see cref="LeetCodeProblemDto"/> instances from the raw JSON strings
    /// stored in a snapshot, merging catalog node data with per-problem content from the
    /// detail responses. Used exclusively on the snapshot replay path where
    /// <see cref="LeetCodeRawCaptureDto.MappedNodes"/> is empty.
    /// </summary>
    private static List<LeetCodeProblemDto> MapRawCaptureToProblems(LeetCodeRawCaptureDto capture)
    {
        var allNodes = new List<LeetCodeQuestionNodeDto>();
        foreach (var rawPage in capture.RawCatalogPages)
        {
            var pageResponse = JsonSerializer.Deserialize<LeetCodeCatalogResponseDto>(rawPage, JsonOptions);
            var nodes = pageResponse?.Data?.ProblemsetQuestionList?.Questions ?? [];
            allNodes.AddRange(nodes);
        }

        return allNodes.Select(node =>
        {
            string? content = null;
            if (capture.RawDetailResponses.TryGetValue(node.TitleSlug, out var rawDetail))
            {
                var detailResponse = JsonSerializer.Deserialize<LeetCodeQuestionDetailResponseDto>(rawDetail, JsonOptions);
                content = detailResponse?.Data?.Question?.Content;
            }

            _ = int.TryParse(node.QuestionFrontendId, out var questionId);
            return new LeetCodeProblemDto
            {
                TitleSlug = node.TitleSlug,
                QuestionId = questionId,
                Title = node.Title,
                Difficulty = node.Difficulty,
                TopicTags = node.TopicTags.Select(t => t.Name).ToList(),
                Content = content
            };
        }).ToList();
    }
}
