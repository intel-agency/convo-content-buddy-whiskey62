using System.Text.Json;
using ConvoContentBuddy.Data.Entities;
using ConvoContentBuddy.Data.Repositories;
using ConvoContentBuddy.Data.Seeder.Models;
using Microsoft.Extensions.Logging;

namespace ConvoContentBuddy.Data.Seeder.Services;

/// <summary>
/// Thin orchestration layer over <see cref="ISnapshotRepository"/> that handles
/// serialization, deserialization, and source-scoped latest-marker management.
/// </summary>
public sealed class SnapshotService : ISnapshotService
{
    /// <summary>
    /// The source identifier used for all snapshot operations in this service.
    /// Must match the value expected by source-scoped repository methods.
    /// </summary>
    public const string SourceIdentifier = "leetcode-graphql";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ISnapshotRepository _snapshotRepository;
    private readonly ILogger<SnapshotService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="SnapshotService"/>.
    /// </summary>
    /// <param name="snapshotRepository">Repository used to persist and query snapshots.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public SnapshotService(ISnapshotRepository snapshotRepository, ILogger<SnapshotService> logger)
    {
        _snapshotRepository = snapshotRepository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task PersistSnapshotAsync(
        IReadOnlyList<LeetCodeQuestionNodeDto> rawNodes,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(rawNodes, JsonOptions);

        var snapshot = new IngestionSnapshot
        {
            Id = Guid.NewGuid(),
            Source = SourceIdentifier,
            CapturedAt = DateTimeOffset.UtcNow,
            ProblemCount = rawNodes.Count,
            Payload = payload,
            IsLatest = false
        };

        await _snapshotRepository.PersistSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
        await _snapshotRepository.MarkAsLatestAsync(snapshot.Id, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Persisted snapshot {SnapshotId} with {Count} raw catalog nodes (source: {Source})",
            snapshot.Id, snapshot.ProblemCount, snapshot.Source);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<LeetCodeQuestionNodeDto>?> LoadLatestSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _snapshotRepository
            .LoadLatestAsync(SourceIdentifier, cancellationToken)
            .ConfigureAwait(false);

        if (snapshot is null)
        {
            _logger.LogDebug("No existing snapshot found for source '{Source}'", SourceIdentifier);
            return null;
        }

        var rawNodes = JsonSerializer.Deserialize<List<LeetCodeQuestionNodeDto>>(snapshot.Payload, JsonOptions);

        _logger.LogInformation(
            "Loaded snapshot {SnapshotId} captured at {CapturedAt} with {Count} raw catalog nodes",
            snapshot.Id, snapshot.CapturedAt, snapshot.ProblemCount);

        return rawNodes;
    }
}
