namespace ConvoContentBuddy.Data.Seeder;

/// <summary>
/// Exception thrown by the ingestion pipeline when data cannot be obtained
/// from any available source (live GraphQL or cached snapshot).
/// </summary>
public sealed class IngestionException : Exception
{
    /// <summary>Initializes a new instance of <see cref="IngestionException"/> with no message.</summary>
    public IngestionException()
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="IngestionException"/> with the specified message.
    /// </summary>
    /// <param name="message">A human-readable description of the error.</param>
    public IngestionException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="IngestionException"/> with a message and an
    /// inner exception that caused this failure.
    /// </summary>
    /// <param name="message">A human-readable description of the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public IngestionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
