using System.Text.Json.Serialization;

namespace ConvoContentBuddy.Data.Seeder.Models;

/// <summary>Represents a single error entry in a GraphQL <c>errors</c> array.</summary>
public sealed class GraphQlErrorDto
{
    /// <summary>Gets or sets the human-readable error message.</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

/// <summary>Top-level GraphQL response envelope for the <c>problemsetQuestionList</c> query.</summary>
public sealed class LeetCodeCatalogResponseDto
{
    /// <summary>Gets or sets the <c>data</c> object returned by the GraphQL response.</summary>
    [JsonPropertyName("data")]
    public LeetCodeCatalogDataDto? Data { get; set; }

    /// <summary>Gets or sets the list of GraphQL errors, if any were returned alongside or instead of data.</summary>
    [JsonPropertyName("errors")]
    public List<GraphQlErrorDto>? Errors { get; set; }
}

/// <summary>The <c>data</c> node of the catalog GraphQL response.</summary>
public sealed class LeetCodeCatalogDataDto
{
    /// <summary>Gets or sets the paginated question list result.</summary>
    [JsonPropertyName("problemsetQuestionList")]
    public LeetCodeQuestionListDto? ProblemsetQuestionList { get; set; }
}

/// <summary>Contains pagination metadata and the page of questions returned by the catalog query.</summary>
public sealed class LeetCodeQuestionListDto
{
    /// <summary>Gets or sets the total number of problems available across all pages.</summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>Gets or sets the list of question nodes for the current page.</summary>
    [JsonPropertyName("questions")]
    public List<LeetCodeQuestionNodeDto> Questions { get; set; } = [];
}

/// <summary>Represents a single problem entry as returned by the catalog GraphQL query.</summary>
public sealed class LeetCodeQuestionNodeDto
{
    /// <summary>Gets or sets the URL-friendly title slug.</summary>
    [JsonPropertyName("titleSlug")]
    public string TitleSlug { get; set; } = string.Empty;

    /// <summary>Gets or sets the frontend-visible question number.</summary>
    [JsonPropertyName("frontendQuestionId")]
    public string QuestionFrontendId { get; set; } = string.Empty;

    /// <summary>Gets or sets the human-readable problem title.</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the difficulty level (Easy, Medium, Hard).</summary>
    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = string.Empty;

    /// <summary>Gets or sets the list of topic tags associated with the problem.</summary>
    [JsonPropertyName("topicTags")]
    public List<LeetCodeTopicTagDto> TopicTags { get; set; } = [];

    /// <summary>
    /// Gets or sets the full problem description HTML. This is <c>null</c> when the node is
    /// constructed from the catalog listing alone; it is populated after a per-problem detail fetch.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

/// <summary>Represents a single topic tag on a LeetCode problem.</summary>
public sealed class LeetCodeTopicTagDto
{
    /// <summary>Gets or sets the display name of the tag (e.g., <c>Array</c>).</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the URL-friendly slug for the tag (e.g., <c>array</c>).</summary>
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;
}

/// <summary>Top-level GraphQL response envelope for the <c>questionData</c> (detail) query.</summary>
public sealed class LeetCodeQuestionDetailResponseDto
{
    /// <summary>Gets or sets the <c>data</c> object returned by the GraphQL response.</summary>
    [JsonPropertyName("data")]
    public LeetCodeQuestionDetailDataDto? Data { get; set; }

    /// <summary>Gets or sets the list of GraphQL errors, if any were returned alongside or instead of data.</summary>
    [JsonPropertyName("errors")]
    public List<GraphQlErrorDto>? Errors { get; set; }
}

/// <summary>The <c>data</c> node of the question detail GraphQL response.</summary>
public sealed class LeetCodeQuestionDetailDataDto
{
    /// <summary>Gets or sets the question detail node.</summary>
    [JsonPropertyName("question")]
    public LeetCodeQuestionDetailDto? Question { get; set; }
}

/// <summary>Contains the detail fields fetched for an individual LeetCode problem.</summary>
public sealed class LeetCodeQuestionDetailDto
{
    /// <summary>Gets or sets the problem description as an HTML string.</summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

/// <summary>
/// The raw snapshot model persisted by <see cref="ConvoContentBuddy.Data.Seeder.Services.SnapshotService"/>.
/// Wraps the aggregated catalog data in a shape that preserves the GraphQL envelope structure
/// (total count + full enriched question list), enabling faithful source replay without losing
/// envelope metadata or unmapped fields.
/// </summary>
public sealed class LeetCodeRawCatalogSnapshotDto
{
    /// <summary>Gets or sets the total number of problems reported by the LeetCode API.</summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>Gets or sets the full list of question nodes with <c>Content</c> populated from detail queries.</summary>
    [JsonPropertyName("questions")]
    public List<LeetCodeQuestionNodeDto> Questions { get; set; } = [];
}
