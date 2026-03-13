using System.Text.Json;
using ConvoContentBuddy.Data.Entities;
using ConvoContentBuddy.Data.Repositories;
using ConvoContentBuddy.Data.Seeder.Models;
using ConvoContentBuddy.Data.Seeder.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ConvoContentBuddy.Tests.Ingestion;

/// <summary>
/// Unit tests for <see cref="SnapshotService"/> covering persistence and retrieval of the full
/// GraphQL catalog envelope.
/// </summary>
public class SnapshotServiceTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static SnapshotService CreateService(Mock<ISnapshotRepository> repoMock)
        => new(repoMock.Object, NullLogger<SnapshotService>.Instance);

    private static List<LeetCodeQuestionNodeDto> BuildNodes(int count = 2, bool withContent = true)
        => Enumerable.Range(1, count)
            .Select(i => new LeetCodeQuestionNodeDto
            {
                TitleSlug = $"slug-{i}",
                QuestionFrontendId = i.ToString(),
                Title = $"Problem {i}",
                Difficulty = "Easy",
                TopicTags = [new LeetCodeTopicTagDto { Name = "Array", Slug = "array" }],
                Content = withContent ? $"<p>Content {i}</p>" : null
            })
            .ToList();

    private static LeetCodeCatalogResponseDto BuildCatalogResponse(int total, List<LeetCodeQuestionNodeDto> questions)
        => new()
        {
            Data = new LeetCodeCatalogDataDto
            {
                ProblemsetQuestionList = new LeetCodeQuestionListDto
                {
                    Total = total,
                    Questions = questions
                }
            }
        };

    /// <summary>
    /// Verifies that <c>PersistSnapshotAsync</c> serializes the full GraphQL catalog envelope
    /// (including the <c>data.problemsetQuestionList</c> wrapper, total count, and Content on
    /// each node) and calls both repository methods with correctly populated data.
    /// </summary>
    [Fact]
    public async Task PersistSnapshotAsync_CallsRepositoryWithCorrectData()
    {
        var nodes = BuildNodes(3, withContent: true);
        var catalogResponse = BuildCatalogResponse(nodes.Count, nodes);
        IngestionSnapshot? capturedSnapshot = null;

        var repo = new Mock<ISnapshotRepository>();
        repo.Setup(r => r.PersistSnapshotAsync(It.IsAny<IngestionSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<IngestionSnapshot, CancellationToken>((s, _) => capturedSnapshot = s)
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.MarkAsLatestAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(repo);
        await service.PersistSnapshotAsync(catalogResponse);

        capturedSnapshot.Should().NotBeNull();
        capturedSnapshot!.Source.Should().Be(SnapshotService.SourceIdentifier);
        capturedSnapshot.ProblemCount.Should().Be(3);

        // Payload must round-trip as the full GraphQL envelope preserving data.problemsetQuestionList and Content
        var deserialized = JsonSerializer.Deserialize<LeetCodeCatalogResponseDto>(capturedSnapshot.Payload, JsonOpts);
        deserialized.Should().NotBeNull();
        deserialized!.Data.Should().NotBeNull();
        deserialized.Data!.ProblemsetQuestionList.Should().NotBeNull();
        deserialized.Data.ProblemsetQuestionList!.Total.Should().Be(3);
        deserialized.Data.ProblemsetQuestionList.Questions.Should().HaveCount(3);
        deserialized.Data.ProblemsetQuestionList.Questions[0].TitleSlug.Should().Be("slug-1");
        deserialized.Data.ProblemsetQuestionList.Questions[0].Content.Should().Be("<p>Content 1</p>");

        repo.Verify(r => r.MarkAsLatestAsync(capturedSnapshot.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that the persisted payload contains the full GraphQL envelope structure with
    /// the <c>data.problemsetQuestionList</c> wrapper, not a flat reconstructed object.
    /// </summary>
    [Fact]
    public async Task PersistSnapshotAsync_PayloadContainsGraphQlEnvelope()
    {
        var nodes = BuildNodes(2, withContent: true);
        var catalogResponse = BuildCatalogResponse(42, nodes);

        IngestionSnapshot? capturedSnapshot = null;
        var repo = new Mock<ISnapshotRepository>();
        repo.Setup(r => r.PersistSnapshotAsync(It.IsAny<IngestionSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<IngestionSnapshot, CancellationToken>((s, _) => capturedSnapshot = s)
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.MarkAsLatestAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(repo);
        await service.PersistSnapshotAsync(catalogResponse);

        capturedSnapshot.Should().NotBeNull();

        // The payload must contain the data.problemsetQuestionList envelope, not a flat { total, questions } object
        using var doc = JsonDocument.Parse(capturedSnapshot!.Payload);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        doc.RootElement.TryGetProperty("data", out var dataProp).Should().BeTrue();
        dataProp.TryGetProperty("problemsetQuestionList", out var pqlProp).Should().BeTrue();
        pqlProp.TryGetProperty("total", out var totalProp).Should().BeTrue();
        totalProp.GetInt32().Should().Be(42);
        pqlProp.TryGetProperty("questions", out _).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that when a snapshot exists, the full GraphQL catalog envelope (including
    /// <c>data.problemsetQuestionList</c>, total count, and Content on each node) is correctly
    /// deserialized and returned.
    /// </summary>
    [Fact]
    public async Task LoadLatestSnapshotAsync_WhenSnapshotExists_ReturnsDeserializedEnvelope()
    {
        var nodes = BuildNodes(2, withContent: true);
        var catalogResponse = BuildCatalogResponse(99, nodes);
        var payload = JsonSerializer.Serialize(catalogResponse, JsonOpts);

        var snapshot = new IngestionSnapshot
        {
            Id = Guid.NewGuid(),
            Source = SnapshotService.SourceIdentifier,
            CapturedAt = DateTimeOffset.UtcNow.AddHours(-1),
            ProblemCount = nodes.Count,
            Payload = payload,
            IsLatest = true
        };

        var repo = new Mock<ISnapshotRepository>();
        repo.Setup(r => r.LoadLatestAsync(SnapshotService.SourceIdentifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var service = CreateService(repo);
        var result = await service.LoadLatestSnapshotAsync();

        result.Should().NotBeNull();
        result!.Data.Should().NotBeNull();
        result.Data!.ProblemsetQuestionList.Should().NotBeNull();
        result.Data.ProblemsetQuestionList!.Total.Should().Be(99);
        result.Data.ProblemsetQuestionList.Questions.Should().HaveCount(2);
        result.Data.ProblemsetQuestionList.Questions[0].TitleSlug.Should().Be("slug-1");
        result.Data.ProblemsetQuestionList.Questions[0].Content.Should().Be("<p>Content 1</p>");
        result.Data.ProblemsetQuestionList.Questions[1].TitleSlug.Should().Be("slug-2");
        result.Data.ProblemsetQuestionList.Questions[1].Content.Should().Be("<p>Content 2</p>");
    }

    /// <summary>
    /// Verifies that <c>null</c> is returned when no snapshot exists for the source.
    /// </summary>
    [Fact]
    public async Task LoadLatestSnapshotAsync_WhenNoSnapshot_ReturnsNull()
    {
        var repo = new Mock<ISnapshotRepository>();
        repo.Setup(r => r.LoadLatestAsync(SnapshotService.SourceIdentifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IngestionSnapshot?)null);

        var service = CreateService(repo);
        var result = await service.LoadLatestSnapshotAsync();

        result.Should().BeNull();
    }
}
