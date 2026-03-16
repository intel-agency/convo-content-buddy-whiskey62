using ConvoContentBuddy.Data.Entities;
using ConvoContentBuddy.Data.Repositories;
using ConvoContentBuddy.Data.Seeder.Models;
using ConvoContentBuddy.Data.Seeder.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;

namespace ConvoContentBuddy.Data.Seeder;

/// <summary>
/// Background service that orchestrates the full problem ingestion pipeline:
/// fetches problems from LeetCode, upserts them into the database, synchronizes tags,
/// generates embeddings for unembedded problems, and then shuts down the host.
/// </summary>
public class ProblemIngestionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IGeminiBatchEmbeddingService _batchEmbeddingService;
    private readonly EmbeddingProfileOptions _embeddingOptions;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<ProblemIngestionWorker> _logger;
    private readonly int _batchThreshold;

    /// <summary>
    /// Initializes a new instance of <see cref="ProblemIngestionWorker"/>.
    /// </summary>
    /// <param name="scopeFactory">Factory used to create a DI scope for scoped services.</param>
    /// <param name="embeddingGenerator">Interactive (single-item) embedding generator.</param>
    /// <param name="batchEmbeddingService">Batch embedding service for large datasets.</param>
    /// <param name="embeddingOptions">Active embedding profile configuration.</param>
    /// <param name="lifetime">Application lifetime used to stop the host after completion.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="configuration">Application configuration (reads <c>Seeder:BatchSize</c>).</param>
    public ProblemIngestionWorker(
        IServiceScopeFactory scopeFactory,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IGeminiBatchEmbeddingService batchEmbeddingService,
        IOptions<EmbeddingProfileOptions> embeddingOptions,
        IHostApplicationLifetime lifetime,
        ILogger<ProblemIngestionWorker> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _embeddingGenerator = embeddingGenerator;
        _batchEmbeddingService = batchEmbeddingService;
        _embeddingOptions = embeddingOptions.Value;
        _lifetime = lifetime;
        _logger = logger;
        _batchThreshold = configuration.GetValue("Seeder:BatchSize", 100);
    }

    /// <summary>
    /// Executes the ingestion pipeline once, then signals the host to stop.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token that signals host shutdown.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var upsertedCount = 0;
        var embeddedCount = 0;

        try
        {
            _logger.LogInformation("Starting problem ingestion pipeline.");

            using var scope = _scopeFactory.CreateScope();
            var dataSource = scope.ServiceProvider.GetRequiredService<ILeetCodeDataSource>();
            var problemRepository = scope.ServiceProvider.GetRequiredService<IProblemRepository>();
            var tagRepository = scope.ServiceProvider.GetRequiredService<ITagRepository>();
            var problemTagRepository = scope.ServiceProvider.GetRequiredService<IProblemTagRepository>();

            // ── Phase 1: Fetch catalog ─────────────────────────────────────────
            var catalog = await dataSource.FetchCatalogAsync(stoppingToken);
            _logger.LogInformation("Fetched {Count} problems from data source.", catalog.Count);

            // ── Phase 2: Upsert problems and sync tags ────────────────────────
            foreach (var dto in catalog)
            {
                stoppingToken.ThrowIfCancellationRequested();

                var problem = new Problem
                {
                    Slug = dto.TitleSlug,
                    Title = dto.Title,
                    Difficulty = dto.Difficulty,
                    Description = dto.Content ?? dto.Title,
                    QuestionId = dto.QuestionId,
                };

                var persisted = await problemRepository.UpsertBySlugAsync(problem, stoppingToken);
                upsertedCount++;

                var tags = await tagRepository.UpsertTagsAsync(dto.TopicTags, stoppingToken);
                var tagIds = tags.Select(t => t.Id);
                await problemTagRepository.SyncTagsAsync(persisted.Id, tagIds, stoppingToken);
            }

            _logger.LogInformation("Upserted {Count} problems and synced tags.", upsertedCount);

            // ── Phase 3: Embed unembedded problems ────────────────────────────
            var unembedded = await problemRepository.GetUnembeddedForProfileAsync(
                _embeddingOptions.ModelName,
                _embeddingOptions.Dimensions,
                stoppingToken);

            _logger.LogInformation(
                "{Count} problems require embedding for profile '{Model}' ({Dims}d).",
                unembedded.Count, _embeddingOptions.ModelName, _embeddingOptions.Dimensions);

            if (unembedded.Count > 0)
            {
                if (unembedded.Count > _batchThreshold)
                {
                    embeddedCount = await EmbedViaBatchAsync(unembedded, problemRepository, stoppingToken);
                }
                else
                {
                    embeddedCount = await EmbedInteractiveAsync(unembedded, problemRepository, stoppingToken);
                }
            }

            _logger.LogInformation(
                "Ingestion pipeline complete. Upserted={Upserted}, Embedded={Embedded}, Skipped={Skipped}.",
                upsertedCount, embeddedCount, unembedded.Count - embeddedCount);
        }
        catch (IngestionException ex)
        {
            _logger.LogError(ex, "Ingestion pipeline failed with an IngestionException.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ingestion pipeline encountered an unexpected error.");
            throw;
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }

    private async Task<int> EmbedViaBatchAsync(
        IReadOnlyList<Problem> problems,
        IProblemRepository problemRepository,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Using batch embedding for {Count} problems.", problems.Count);

        var items = problems
            .Select(p => (p.Id, BuildEmbeddingText(p)))
            .ToList();

        var results = await _batchEmbeddingService.EmbedBatchAsync(items, cancellationToken);

        var generatedAt = DateTimeOffset.UtcNow;
        foreach (var (problemId, floatArray) in results)
        {
            await problemRepository.UpdateEmbeddingAsync(
                problemId,
                new Vector(floatArray),
                _embeddingOptions.ModelName,
                _embeddingOptions.Dimensions,
                generatedAt,
                cancellationToken);
        }

        return results.Count;
    }

    private async Task<int> EmbedInteractiveAsync(
        IReadOnlyList<Problem> problems,
        IProblemRepository problemRepository,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Using interactive embedding for {Count} problems.", problems.Count);

        var texts = problems.Select(BuildEmbeddingText).ToList();
        var generatedEmbeddings = await _embeddingGenerator.GenerateAsync(texts, cancellationToken: cancellationToken);

        var generatedAt = DateTimeOffset.UtcNow;
        for (var i = 0; i < problems.Count && i < generatedEmbeddings.Count; i++)
        {
            await problemRepository.UpdateEmbeddingAsync(
                problems[i].Id,
                new Vector(generatedEmbeddings[i].Vector.ToArray()),
                _embeddingOptions.ModelName,
                _embeddingOptions.Dimensions,
                generatedAt,
                cancellationToken);
        }

        return Math.Min(problems.Count, generatedEmbeddings.Count);
    }

    private static string BuildEmbeddingText(Problem problem) =>
        $"{problem.Title} {problem.Difficulty} {problem.Description}";
}
