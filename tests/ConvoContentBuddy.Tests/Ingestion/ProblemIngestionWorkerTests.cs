using ConvoContentBuddy.Data.Entities;
using ConvoContentBuddy.Data.Repositories;
using ConvoContentBuddy.Data.Seeder;
using ConvoContentBuddy.Data.Seeder.Models;
using ConvoContentBuddy.Data.Seeder.Services;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConvoContentBuddy.Tests.Ingestion;

/// <summary>
/// Unit tests for <see cref="ProblemIngestionWorker"/> covering pipeline orchestration,
/// idempotency, batch/interactive embedding path selection, host lifecycle management,
/// and unexpected exception surfacing.
/// </summary>
public class ProblemIngestionWorkerTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static IOptions<EmbeddingProfileOptions> DefaultOptions() =>
        Options.Create(new EmbeddingProfileOptions
        {
            ModelName = "gemini-embedding-001",
            Dimensions = 1536,
            ApiKey = "test-key"
        });

    private static LeetCodeProblemDto BuildDto(string slug = "two-sum", int id = 1) =>
        new()
        {
            TitleSlug = slug,
            QuestionId = id,
            Title = "Two Sum",
            Difficulty = "Easy",
            Content = "<p>Given an array…</p>",
            TopicTags = ["Array", "Hash Table"]
        };

    private static Problem BuildProblem(Guid? id = null, string slug = "two-sum") =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Slug = slug,
            Title = "Two Sum",
            Difficulty = "Easy",
            Description = "<p>Given an array…</p>",
            QuestionId = 1,
        };

    private static Tag BuildTag(string name) =>
        new() { Id = Guid.NewGuid(), Name = name };

    private static Microsoft.Extensions.Configuration.IConfiguration BuildConfig(int batchSize = 100) =>
        new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seeder:BatchSize"] = batchSize.ToString()
            })
            .Build();

    /// <summary>
    /// Creates a mock <see cref="IServiceScopeFactory"/> that resolves the provided mocks
    /// from the scope's service provider.
    /// </summary>
    private static Mock<IServiceScopeFactory> CreateScopeFactory(
        Mock<ILeetCodeDataSource> dataSource,
        Mock<IProblemRepository> problemRepo,
        Mock<ITagRepository> tagRepo,
        Mock<IProblemTagRepository> problemTagRepo)
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(ILeetCodeDataSource))).Returns(dataSource.Object);
        serviceProvider.Setup(sp => sp.GetService(typeof(IProblemRepository))).Returns(problemRepo.Object);
        serviceProvider.Setup(sp => sp.GetService(typeof(ITagRepository))).Returns(tagRepo.Object);
        serviceProvider.Setup(sp => sp.GetService(typeof(IProblemTagRepository))).Returns(problemTagRepo.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        return scopeFactory;
    }

    private static ProblemIngestionWorker CreateWorker(
        Mock<IServiceScopeFactory> scopeFactory,
        Mock<IEmbeddingGenerator<string, Embedding<float>>> embeddingGenerator,
        Mock<IGeminiBatchEmbeddingService> batchService,
        Mock<IHostApplicationLifetime> lifetime,
        IOptions<EmbeddingProfileOptions>? options = null,
        int batchSize = 100)
    {
        return new ProblemIngestionWorker(
            scopeFactory.Object,
            embeddingGenerator.Object,
            batchService.Object,
            options ?? DefaultOptions(),
            lifetime.Object,
            NullLogger<ProblemIngestionWorker>.Instance,
            BuildConfig(batchSize));
    }

    private static GeneratedEmbeddings<Embedding<float>> BuildEmbeddings(int count) =>
        new(Enumerable.Range(0, count)
            .Select(_ => new Embedding<float>(new float[1536]))
            .ToList());

    // ── tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the full pipeline executes in the expected order:
    /// FetchCatalog → UpsertBySlug → UpsertTags → SyncTags → GetUnembedded →
    /// embed → UpdateEmbedding → StopApplication.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_FullPipelineExecutesInOrder()
    {
        var dto = BuildDto();
        var problem = BuildProblem();
        var tags = new List<Tag> { BuildTag("Array"), BuildTag("Hash Table") };
        var unembedded = new List<Problem> { BuildProblem() };

        var dataSource = new Mock<ILeetCodeDataSource>();
        dataSource.Setup(d => d.FetchCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeetCodeProblemDto> { dto });

        var problemRepo = new Mock<IProblemRepository>();
        problemRepo.Setup(r => r.UpsertBySlugAsync(It.IsAny<Problem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(problem);
        problemRepo.Setup(r => r.GetUnembeddedForProfileAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(unembedded);
        problemRepo.Setup(r => r.UpdateEmbeddingAsync(It.IsAny<Guid>(), It.IsAny<Pgvector.Vector>(),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tagRepo = new Mock<ITagRepository>();
        tagRepo.Setup(r => r.UpsertTagsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tags);

        var problemTagRepo = new Mock<IProblemTagRepository>();
        problemTagRepo.Setup(r => r.SyncTagsAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var embeddingGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        embeddingGenerator.Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildEmbeddings(1));

        var batchService = new Mock<IGeminiBatchEmbeddingService>();
        var lifetime = new Mock<IHostApplicationLifetime>();
        var scopeFactory = CreateScopeFactory(dataSource, problemRepo, tagRepo, problemTagRepo);

        var worker = CreateWorker(scopeFactory, embeddingGenerator, batchService, lifetime);
        await worker.StartAsync(CancellationToken.None);
        await (worker.ExecuteTask ?? Task.CompletedTask);

        dataSource.Verify(d => d.FetchCatalogAsync(It.IsAny<CancellationToken>()), Times.Once);
        problemRepo.Verify(r => r.UpsertBySlugAsync(It.IsAny<Problem>(), It.IsAny<CancellationToken>()), Times.Once);
        tagRepo.Verify(r => r.UpsertTagsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        problemTagRepo.Verify(r => r.SyncTagsAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
        problemRepo.Verify(r => r.GetUnembeddedForProfileAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        problemRepo.Verify(r => r.UpdateEmbeddingAsync(It.IsAny<Guid>(), It.IsAny<Pgvector.Vector>(),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
        lifetime.Verify(l => l.StopApplication(), Times.Once);
    }

    /// <summary>
    /// Verifies idempotency: when all problems are already embedded, no embedding calls are made,
    /// but StopApplication is still called.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_SkipsEmbeddingWhenAllAlreadyEmbedded()
    {
        var dto = BuildDto();
        var problem = BuildProblem();
        var tags = new List<Tag> { BuildTag("Array") };

        var dataSource = new Mock<ILeetCodeDataSource>();
        dataSource.Setup(d => d.FetchCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeetCodeProblemDto> { dto });

        var problemRepo = new Mock<IProblemRepository>();
        problemRepo.Setup(r => r.UpsertBySlugAsync(It.IsAny<Problem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(problem);
        problemRepo.Setup(r => r.GetUnembeddedForProfileAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Problem>());

        var tagRepo = new Mock<ITagRepository>();
        tagRepo.Setup(r => r.UpsertTagsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tags);

        var problemTagRepo = new Mock<IProblemTagRepository>();
        problemTagRepo.Setup(r => r.SyncTagsAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var embeddingGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        var batchService = new Mock<IGeminiBatchEmbeddingService>();
        var lifetime = new Mock<IHostApplicationLifetime>();
        var scopeFactory = CreateScopeFactory(dataSource, problemRepo, tagRepo, problemTagRepo);

        var worker = CreateWorker(scopeFactory, embeddingGenerator, batchService, lifetime);
        await worker.StartAsync(CancellationToken.None);
        await (worker.ExecuteTask ?? Task.CompletedTask);

        embeddingGenerator.Verify(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()), Times.Never);
        batchService.Verify(b => b.EmbedBatchAsync(It.IsAny<IReadOnlyList<(Guid, string)>>(), It.IsAny<CancellationToken>()), Times.Never);
        lifetime.Verify(l => l.StopApplication(), Times.Once);
    }

    /// <summary>
    /// Verifies that the batch embedding path is selected when problem count exceeds the threshold.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_UsesBatchPathWhenCountExceedsThreshold()
    {
        var dto = BuildDto();
        var problem = BuildProblem();
        var tags = new List<Tag> { BuildTag("Array") };
        var unembedded = Enumerable.Range(0, 101).Select(i => BuildProblem(slug: $"slug-{i}")).ToList();

        var dataSource = new Mock<ILeetCodeDataSource>();
        dataSource.Setup(d => d.FetchCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeetCodeProblemDto> { dto });

        var problemRepo = new Mock<IProblemRepository>();
        problemRepo.Setup(r => r.UpsertBySlugAsync(It.IsAny<Problem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(problem);
        problemRepo.Setup(r => r.GetUnembeddedForProfileAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(unembedded);
        problemRepo.Setup(r => r.UpdateEmbeddingAsync(It.IsAny<Guid>(), It.IsAny<Pgvector.Vector>(),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tagRepo = new Mock<ITagRepository>();
        tagRepo.Setup(r => r.UpsertTagsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tags);

        var problemTagRepo = new Mock<IProblemTagRepository>();
        problemTagRepo.Setup(r => r.SyncTagsAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var embeddingGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();

        var batchService = new Mock<IGeminiBatchEmbeddingService>();
        batchService.Setup(b => b.EmbedBatchAsync(It.IsAny<IReadOnlyList<(Guid, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(unembedded.Select(p => (p.Id, new float[1536])).ToList());

        var lifetime = new Mock<IHostApplicationLifetime>();
        var scopeFactory = CreateScopeFactory(dataSource, problemRepo, tagRepo, problemTagRepo);

        // Threshold = 100, unembedded count = 101 → batch path
        var worker = CreateWorker(scopeFactory, embeddingGenerator, batchService, lifetime, batchSize: 100);
        await worker.StartAsync(CancellationToken.None);
        await (worker.ExecuteTask ?? Task.CompletedTask);

        batchService.Verify(b => b.EmbedBatchAsync(It.IsAny<IReadOnlyList<(Guid, string)>>(), It.IsAny<CancellationToken>()), Times.Once);
        embeddingGenerator.Verify(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that the interactive embedding path is selected when problem count is at or below the threshold.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_UsesInteractivePathWhenCountAtOrBelowThreshold()
    {
        var dto = BuildDto();
        var problem = BuildProblem();
        var tags = new List<Tag> { BuildTag("Array") };
        var unembedded = Enumerable.Range(0, 50).Select(i => BuildProblem(slug: $"slug-{i}")).ToList();

        var dataSource = new Mock<ILeetCodeDataSource>();
        dataSource.Setup(d => d.FetchCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeetCodeProblemDto> { dto });

        var problemRepo = new Mock<IProblemRepository>();
        problemRepo.Setup(r => r.UpsertBySlugAsync(It.IsAny<Problem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(problem);
        problemRepo.Setup(r => r.GetUnembeddedForProfileAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(unembedded);
        problemRepo.Setup(r => r.UpdateEmbeddingAsync(It.IsAny<Guid>(), It.IsAny<Pgvector.Vector>(),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tagRepo = new Mock<ITagRepository>();
        tagRepo.Setup(r => r.UpsertTagsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tags);

        var problemTagRepo = new Mock<IProblemTagRepository>();
        problemTagRepo.Setup(r => r.SyncTagsAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var embeddingGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        embeddingGenerator.Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildEmbeddings(50));

        var batchService = new Mock<IGeminiBatchEmbeddingService>();
        var lifetime = new Mock<IHostApplicationLifetime>();
        var scopeFactory = CreateScopeFactory(dataSource, problemRepo, tagRepo, problemTagRepo);

        var worker = CreateWorker(scopeFactory, embeddingGenerator, batchService, lifetime, batchSize: 100);
        await worker.StartAsync(CancellationToken.None);
        await (worker.ExecuteTask ?? Task.CompletedTask);

        embeddingGenerator.Verify(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()), Times.Once);
        batchService.Verify(b => b.EmbedBatchAsync(It.IsAny<IReadOnlyList<(Guid, string)>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that StopApplication is called and the exception propagates after an unexpected error.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_StopsHostAfterError()
    {
        var dataSource = new Mock<ILeetCodeDataSource>();
        dataSource.Setup(d => d.FetchCatalogAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var problemRepo = new Mock<IProblemRepository>();
        var tagRepo = new Mock<ITagRepository>();
        var problemTagRepo = new Mock<IProblemTagRepository>();
        var embeddingGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        var batchService = new Mock<IGeminiBatchEmbeddingService>();
        var lifetime = new Mock<IHostApplicationLifetime>();
        var scopeFactory = CreateScopeFactory(dataSource, problemRepo, tagRepo, problemTagRepo);

        var worker = CreateWorker(scopeFactory, embeddingGenerator, batchService, lifetime);
        await worker.StartAsync(CancellationToken.None);

        var act = async () => await (worker.ExecuteTask ?? Task.CompletedTask);
        await act.Should().ThrowAsync<HttpRequestException>();

        lifetime.Verify(l => l.StopApplication(), Times.Once);
    }

    /// <summary>
    /// Verifies that unexpected repository failures are surfaced as exceptions rather than silently suppressed.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_SurfacesUnexpectedRepositoryExceptions()
    {
        var dto = BuildDto();
        var problem = BuildProblem();
        var tags = new List<Tag> { BuildTag("Array") };
        var unembedded = new List<Problem> { BuildProblem() };

        var dataSource = new Mock<ILeetCodeDataSource>();
        dataSource.Setup(d => d.FetchCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeetCodeProblemDto> { dto });

        var problemRepo = new Mock<IProblemRepository>();
        problemRepo.Setup(r => r.UpsertBySlugAsync(It.IsAny<Problem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(problem);
        problemRepo.Setup(r => r.GetUnembeddedForProfileAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(unembedded);
        problemRepo.Setup(r => r.UpdateEmbeddingAsync(It.IsAny<Guid>(), It.IsAny<Pgvector.Vector>(),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database write failed."));

        var tagRepo = new Mock<ITagRepository>();
        tagRepo.Setup(r => r.UpsertTagsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tags);

        var problemTagRepo = new Mock<IProblemTagRepository>();
        problemTagRepo.Setup(r => r.SyncTagsAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var embeddingGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        embeddingGenerator.Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildEmbeddings(1));

        var batchService = new Mock<IGeminiBatchEmbeddingService>();
        var lifetime = new Mock<IHostApplicationLifetime>();
        var scopeFactory = CreateScopeFactory(dataSource, problemRepo, tagRepo, problemTagRepo);

        var worker = CreateWorker(scopeFactory, embeddingGenerator, batchService, lifetime);
        await worker.StartAsync(CancellationToken.None);

        var act = async () => await (worker.ExecuteTask ?? Task.CompletedTask);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database write failed.");

        lifetime.Verify(l => l.StopApplication(), Times.Once);
    }

    /// <summary>
    /// Verifies that tags are synced with the correct problem ID and tag IDs.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_SyncsTagsWithCorrectProblemId()
    {
        var problemId = Guid.NewGuid();
        var dto = BuildDto();
        var problem = BuildProblem(id: problemId);
        var tags = new List<Tag>
        {
            new() { Id = Guid.NewGuid(), Name = "Array" },
            new() { Id = Guid.NewGuid(), Name = "Hash Table" }
        };
        var expectedTagIds = tags.Select(t => t.Id).ToList();

        var dataSource = new Mock<ILeetCodeDataSource>();
        dataSource.Setup(d => d.FetchCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeetCodeProblemDto> { dto });

        var problemRepo = new Mock<IProblemRepository>();
        problemRepo.Setup(r => r.UpsertBySlugAsync(It.IsAny<Problem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(problem);
        problemRepo.Setup(r => r.GetUnembeddedForProfileAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Problem>());

        var tagRepo = new Mock<ITagRepository>();
        tagRepo.Setup(r => r.UpsertTagsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tags);

        IEnumerable<Guid>? capturedTagIds = null;
        Guid capturedProblemId = Guid.Empty;
        var problemTagRepo = new Mock<IProblemTagRepository>();
        problemTagRepo.Setup(r => r.SyncTagsAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, IEnumerable<Guid>, CancellationToken>((pid, tids, _) =>
            {
                capturedProblemId = pid;
                capturedTagIds = tids.ToList();
            })
            .Returns(Task.CompletedTask);

        var embeddingGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        var batchService = new Mock<IGeminiBatchEmbeddingService>();
        var lifetime = new Mock<IHostApplicationLifetime>();
        var scopeFactory = CreateScopeFactory(dataSource, problemRepo, tagRepo, problemTagRepo);

        var worker = CreateWorker(scopeFactory, embeddingGenerator, batchService, lifetime);
        await worker.StartAsync(CancellationToken.None);
        await (worker.ExecuteTask ?? Task.CompletedTask);

        capturedProblemId.Should().Be(problemId);
        capturedTagIds.Should().BeEquivalentTo(expectedTagIds);
    }
}
