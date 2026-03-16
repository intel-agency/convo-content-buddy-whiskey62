namespace ConvoContentBuddy.Data.Seeder.Models;

/// <summary>
/// Configuration options for the active embedding profile, bound from the <c>EmbeddingProfile</c>
/// configuration section.
/// </summary>
public class EmbeddingProfileOptions
{
    /// <summary>Gets or sets the embedding model name.</summary>
    public string ModelName { get; set; } = "gemini-embedding-001";

    /// <summary>Gets or sets the number of embedding dimensions.</summary>
    public int Dimensions { get; set; } = 1536;

    /// <summary>Gets or sets the Gemini API key.</summary>
    public string ApiKey { get; set; } = string.Empty;
}
