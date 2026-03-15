extern alias BrainAssembly;

using BrainAssembly::ConvoContentBuddy.API.Brain.Models;
using ConvoContentBuddy.Data;
using ConvoContentBuddy.Data.Entities;
using ConvoContentBuddy.Data.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Pgvector;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ConvoContentBuddy.Tests.Api;

/// <summary>
/// Integration tests for the <c>/api/problems</c> endpoints using a custom
/// <see cref="WebApplicationFactory{TEntryPoint}"/> that replaces real services with mocks.
/// </summary>
public class ProblemSearchEndpointTests : IDisposable
{
    private readonly Mock<IProblemRepository> _mockRepo = new();
    private readonly Mock<IEmbeddingGenerator<string, Embedding<float>>> _mockGen = new();
    private readonly WebApplicationFactory<BrainAssembly::Program> _factory;
    private readonly HttpClient _client;

    /// <summary>Initializes the test factory with mocked services and an in-memory database.</summary>
    public ProblemSearchEndpointTests()
    {
        var dbName = Guid.NewGuid().ToString();

        _factory = new WebApplicationFactory<BrainAssembly::Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:convocontentbuddy"] = "Host=localhost;Database=test",
                    ["EmbeddingProfile:ModelName"] = "gemini-embedding-001",
                    ["EmbeddingProfile:Dimensions"] = "1536",
                    ["EmbeddingProfile:ApiKey"] = "test-key",
                });
            });

            builder.ConfigureServices(services =>
            {
                // Remove ALL Aspire-registered services related to AppDbContext
                // (including the context pool, scoped lease, and options registered by AddNpgsqlDbContext).
                var toRemove = services
                    .Where(d =>
                        (d.ServiceType.FullName?.Contains("AppDbContext") == true) ||
                        (d.ImplementationType?.FullName?.Contains("AppDbContext") == true) ||
                        d.ServiceType == typeof(DbContextOptions<AppDbContext>))
                    .ToList();
                foreach (var d in toRemove)
                    services.Remove(d);

                services.AddScoped<AppDbContext>(sp =>
                {
                    var options = new DbContextOptionsBuilder<AppDbContext>()
                        .UseInMemoryDatabase(dbName)
                        .Options;
                    return new TestAppDbContext(options);
                });

                // Replace IProblemRepository with mock.
                RemoveService<IProblemRepository>(services);
                services.AddScoped<IProblemRepository>(_ => _mockRepo.Object);

                // Replace IEmbeddingGenerator with mock.
                RemoveService<IEmbeddingGenerator<string, Embedding<float>>>(services);
                services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
                    _ => _mockGen.Object);
            });
        });

        _client = _factory.CreateClient();
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(T));
        if (descriptor != null)
            services.Remove(descriptor);
    }

    private void SeedCorpus()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ctx.Problems.Add(new Problem
        {
            Id = Guid.NewGuid(),
            Slug = "corpus-seed",
            QuestionId = 999,
            Title = "Corpus Seed",
            Difficulty = "Easy",
            Description = "Seed problem for corpus check.",
            EmbeddingModel = "gemini-embedding-001",
            EmbeddingDimensions = 1536,
            Embedding = new Vector(new float[1536]),
            SeededAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        ctx.SaveChanges();
    }

    private void SeedCorpusNoEmbedding()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ctx.Problems.Add(new Problem
        {
            Id = Guid.NewGuid(),
            Slug = "corpus-seed-no-embedding",
            QuestionId = 998,
            Title = "Corpus Seed No Embedding",
            Difficulty = "Easy",
            Description = "Seed problem with profile metadata but no embedding vector.",
            EmbeddingModel = "gemini-embedding-001",
            EmbeddingDimensions = 1536,
            Embedding = null,
            SeededAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        ctx.SaveChanges();
    }

    /// <summary>
    /// Verifies that a successful search returns the expected DTO shape and that no
    /// raw embedding data is exposed in the response.
    /// </summary>
    [Fact]
    public async Task Search_ReturnsCorrectDtoShape_WhenCorpusAvailable()
    {
        // Arrange
        SeedCorpus();

        var tag = new Tag { Id = Guid.NewGuid(), Name = "Array" };
        var problem = new Problem
        {
            Id = Guid.NewGuid(),
            Slug = "two-sum",
            QuestionId = 1,
            Title = "Two Sum",
            Difficulty = "Easy",
            Description = "Find two numbers that add up to target.",
            EmbeddingModel = "gemini-embedding-001",
            EmbeddingDimensions = 1536,
            Embedding = new Vector(new float[1536]),
            SeededAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            ProblemTags = [new ProblemTag { Tag = tag, TagId = tag.Id, ProblemId = Guid.NewGuid() }],
        };

        _mockRepo.Setup(r => r.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(10);
        _mockRepo.Setup(r => r.SearchByVectorAsync(
                It.IsAny<Vector>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Problem>)[problem]);

        _mockGen.Setup(g => g.GenerateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>(
                [new Embedding<float>(new float[1536])]));

        // Act
        var response = await _client.GetAsync("/api/problems/search?q=two+sum");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var root = json!.RootElement;
        root.ValueKind.Should().Be(JsonValueKind.Array);
        root.GetArrayLength().Should().Be(1);

        var first = root[0];
        first.TryGetProperty("slug", out _).Should().BeTrue();
        first.TryGetProperty("title", out _).Should().BeTrue();
        first.TryGetProperty("difficulty", out _).Should().BeTrue();
        first.TryGetProperty("tags", out _).Should().BeTrue();
        first.TryGetProperty("similarityScore", out _).Should().BeTrue();
        first.TryGetProperty("embedding", out _).Should().BeFalse("raw embedding must not be exposed");
    }

    /// <summary>
    /// Verifies that the search endpoint returns 503 when the corpus is empty (CountAsync returns 0).
    /// </summary>
    [Fact]
    public async Task Search_Returns503_WhenNoCorpus()
    {
        // Arrange
        _mockRepo.Setup(r => r.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        // Act
        var response = await _client.GetAsync("/api/problems/search?q=binary+search");

        // Assert
        ((int)response.StatusCode).Should().Be(503);

        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        json!.RootElement.TryGetProperty("error", out _).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that the search endpoint returns 503 when rows match the active embedding profile's
    /// model and dimensions but none have a stored embedding vector (<c>Embedding == null</c>).
    /// This ensures that partially-seeded rows do not bypass the corpus-availability guard.
    /// </summary>
    [Fact]
    public async Task Search_Returns503_WhenProfileMatchesButNoEmbedding()
    {
        // Arrange — seed a row with profile metadata but no embedding vector.
        SeedCorpusNoEmbedding();

        // CountAsync reports the row exists so the first (total-count) guard passes.
        _mockRepo.Setup(r => r.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var response = await _client.GetAsync("/api/problems/search?q=binary+search");

        // Assert — the second guard (Embedding != null) must still reject the request.
        ((int)response.StatusCode).Should().Be(503);

        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        json!.RootElement.TryGetProperty("error", out _).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that the count endpoint returns a JSON object containing the correct integer count.
    /// </summary>
    [Fact]
    public async Task Count_ReturnsCorrectInteger()
    {
        // Arrange
        _mockRepo.Setup(r => r.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(42);

        // Act
        var response = await _client.GetAsync("/api/problems/count");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var root = json!.RootElement;
        root.TryGetProperty("count", out var countProp).Should().BeTrue();
        countProp.GetInt32().Should().Be(42);
    }

    /// <summary>
    /// Verifies that calling the search endpoint without a query parameter returns 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task Search_Returns400_WhenQueryParameterMissing()
    {
        // Act
        var response = await _client.GetAsync("/api/problems/search");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        json!.RootElement.TryGetProperty("error", out _).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that the <c>limit</c> query parameter is clamped to a maximum of 20
    /// even when a larger value is supplied.
    /// </summary>
    [Fact]
    public async Task Search_ClampsLimitToMax20_WhenLargerValueSupplied()
    {
        // Arrange
        SeedCorpus();

        _mockRepo.Setup(r => r.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(10);
        _mockRepo.Setup(r => r.SearchByVectorAsync(
                It.IsAny<Vector>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Problem>)[]);

        _mockGen.Setup(g => g.GenerateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>(
                [new Embedding<float>(new float[1536])]));

        // Act
        await _client.GetAsync("/api/problems/search?q=sort&limit=100");

        // Assert — verify that SearchByVectorAsync was called with limit clamped to ≤ 20.
        _mockRepo.Verify(r => r.SearchByVectorAsync(
            It.IsAny<Vector>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.Is<int>(l => l <= 20),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    /// <summary>
    /// Test-only <see cref="AppDbContext"/> subclass that adds a string value converter for
    /// <see cref="Problem.Embedding"/> so the InMemory provider can store and query it.
    /// </summary>
    private sealed class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Problem>()
                .Property(p => p.Embedding)
                .HasConversion(
                    v => v == null ? null : string.Join(",", v.ToArray()),
                    s => s == null ? null : new Vector(
                        s.Split(',', StringSplitOptions.RemoveEmptyEntries)
                         .Select(float.Parse)
                         .ToArray()));
        }
    }
}
