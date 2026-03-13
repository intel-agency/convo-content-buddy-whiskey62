namespace ConvoContentBuddy.Data.Entities;

/// <summary>
/// Join entity representing the many-to-many relationship between <see cref="Problem"/> and <see cref="Tag"/>.
/// </summary>
public class ProblemTag
{
    /// <summary>Gets or sets the FK referencing <see cref="Problem.Id"/>.</summary>
    public Guid ProblemId { get; set; }

    /// <summary>Gets or sets the FK referencing <see cref="Tag.Id"/>.</summary>
    public Guid TagId { get; set; }

    /// <summary>Gets or sets the associated problem.</summary>
    public Problem Problem { get; set; } = null!;

    /// <summary>Gets or sets the associated tag.</summary>
    public Tag Tag { get; set; } = null!;
}
