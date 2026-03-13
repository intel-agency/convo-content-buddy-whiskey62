using System.Text.Json;
using ConvoContentBuddy.Data.Entities;
using ConvoContentBuddy.Data.Repositories;
using ConvoContentBuddy.Data.Seeder.Models;
using ConvoContentBuddy.Data.Seeder.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ConvoContentBuddy.Tests.Ingestion;

/// <summary>
/// Unit tests for <see cref="SnapshotService"/> covering persistence and retrieval of raw catalog nodes.
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

    /// <summary>
    /// Verifies that <c>PersistSnapshotAsync</c> serializes the raw nodes (including Content) and
    /// calls both repository methods with correctly populated data.
    /// </summary>
    [Fact]
    public async Task PersistSnapshotAsync_CallsRepositoryWithCorrectData()
    {
        var nodes = BuildNodes(3, withContent: true);
        IngestionSnapshot? capturedSnapshot = null;

        var repo = new Mock<ISnapshotRepository>();
        repo.Setup(r => r.PersistSnapshotAsync(It.IsAny<IngestionSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<IngestionSnapshot, CancellationToken>((s, _) => capturedSnapshot = s)
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.MarkAsLatestAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(repo);
        await service.PersistSnapshotAsync(nodes);

        capturedSnapshot.Should().NotBeNull();
        capturedSnapshot!.Source.Should().Be(SnapshotService.SourceIdentifier);
        capturedSnapshot.ProblemCount.Should().Be(3);

        // Payload must round-trip as raw nodes preserving Content
        var deserialized = JsonSerializer.Deserialize<List<LeetCodeQuestionNodeDto>>(capturedSnapshot.Payload, JsonOpts);
        deserialized.Should().HaveCount(3);
        deserialized![0].TitleSlug.Should().Be("slug-1");
        deserialized[0].Content.Should().Be("<p>Content 1</p>");

        repo.Verify(r => r.MarkAsLatestAsync(capturedSnapshot.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that when a snapshot exists, the raw nodes (including Content) are correctly
    /// deserialized and returned.
    /// </summary>
    [Fact]
    public async Task LoadLatestSnapshotAsync_WhenSnapshotExists_ReturnsDeserializedNodes()
    {
        var nodes = BuildNodes(2, withContent: true);
        var payload = JsonSerializer.Serialize(nodes, JsonOpts);

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
        result!.Should().HaveCount(2);
        result[0].TitleSlug.Should().Be("slug-1");
        result[0].Content.Should().Be("<p>Content 1</p>");
        result[1].TitleSlug.Should().Be("slug-2");
        result[1].Content.Should().Be("<p>Content 2</p>");
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
