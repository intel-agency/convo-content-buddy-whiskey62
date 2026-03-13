namespace ConvoContentBuddy.Data.Entities;

/// <summary>
/// Records a point-in-time snapshot of ingested problem data.
/// </summary>
public class IngestionSnapshot
{
    /// <summary>Gets or sets the primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the data source identifier.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets or sets when the snapshot was captured.</summary>
    public DateTimeOffset CapturedAt { get; set; }

    /// <summary>Gets or sets the number of problems in this snapshot.</summary>
    public int ProblemCount { get; set; }

    /// <summary>Gets or sets the raw snapshot payload serialised as JSON (stored as JSONB).</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether this is the most recent snapshot for the source.</summary>
    public bool IsLatest { get; set; }
}
