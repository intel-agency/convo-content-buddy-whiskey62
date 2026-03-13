namespace ConvoContentBuddy.Data.Seeder.Models;

/// <summary>
/// Configuration options for the LeetCode GraphQL HTTP client.
/// Bind from <c>appsettings.json</c> via <c>IOptions&lt;LeetCodeClientOptions&gt;</c>.
/// </summary>
public sealed class LeetCodeClientOptions
{
    /// <summary>Gets or sets the delay in milliseconds between paginated catalog requests. Defaults to 750.</summary>
    public int DelayBetweenRequestsMs { get; set; } = 750;

    /// <summary>Gets or sets the number of problems to fetch per catalog page. Defaults to 100.</summary>
    public int PageSize { get; set; } = 100;

    /// <summary>Gets or sets the maximum number of retry attempts on transient failures. Defaults to 3.</summary>
    public int MaxRetryAttempts { get; set; } = 3;
}
