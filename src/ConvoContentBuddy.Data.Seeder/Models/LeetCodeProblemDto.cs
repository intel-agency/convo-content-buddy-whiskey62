using System.Text.Json.Serialization;

namespace ConvoContentBuddy.Data.Seeder.Models;

/// <summary>
/// Flat data-transfer object representing a single LeetCode problem,
/// combining catalog metadata with optional problem content.
/// </summary>
public sealed class LeetCodeProblemDto
{
    /// <summary>Gets or sets the URL-friendly title slug (e.g., <c>two-sum</c>).</summary>
    [JsonPropertyName("titleSlug")]
    public string TitleSlug { get; set; } = string.Empty;

    /// <summary>Gets or sets the frontend-visible problem number (e.g., 1 for Two Sum).</summary>
    [JsonPropertyName("questionId")]
    public int QuestionId { get; set; }

    /// <summary>Gets or sets the human-readable problem title.</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the difficulty level (Easy, Medium, Hard).</summary>
    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the flat list of topic tag names extracted from the GraphQL
    /// <c>topicTags[].name</c> field.
    /// </summary>
    [JsonPropertyName("topicTags")]
    public List<string> TopicTags { get; set; } = [];

    /// <summary>
    /// Gets or sets the problem description HTML. This is <c>null</c> when the DTO is
    /// constructed from the catalog listing; it is populated only after a per-problem
    /// detail fetch.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
