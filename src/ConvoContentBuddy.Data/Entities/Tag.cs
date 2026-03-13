namespace ConvoContentBuddy.Data.Entities;

/// <summary>
/// Represents a classification tag that can be associated with many problems.
/// </summary>
public class Tag
{
    /// <summary>Gets or sets the primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the unique tag name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the associated problem-tag join records.</summary>
    public ICollection<ProblemTag> ProblemTags { get; set; } = [];
}
