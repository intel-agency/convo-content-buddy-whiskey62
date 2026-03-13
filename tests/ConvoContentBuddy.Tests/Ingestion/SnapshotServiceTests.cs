using System.Text.Json;
using ConvoContentBuddy.Data.Entities;
using ConvoContentBuddy.Data.Repositories;
using ConvoContentBuddy.Data.Seeder.Models;
using ConvoContentBuddy.Data.Seeder.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ConvoContentBuddy.Tests.Ingestion;

/// <summary>
/// Unit tests for <see cref="SnapshotService"/> covering persistence and retrieval of the raw
/// GraphQL capture including unmapped field survival through the snapshot round-trip.
/// </summary>
public class SnapshotServiceTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static SnapshotService CreateService(Mock<ISnapshotRepository> repoMock)
        => new(repoMock.Object, NullLogger<SnapshotService>.Instance);

    private static LeetCodeRawCaptureDto BuildCapture(
        int nodeCount = 2,
        bool withContent = true,
        Dictionary<string, object>? extraCatalogFields = null)
    {
        var nodes = Enumerable.Range(1, nodeCount)
            .Select(i => (object)new
            {
                titleSlug = $"slug-{i}",
                frontendQuestionId = i.ToString(),
                title = $"Problem {i}",
                difficulty = "Easy",
                topicTags = new[] { new { name = "Array", slug = "array" } }
            })
            .ToList();

        var catalogData = new Dictionary<string, object>
        {
            ["data"] = new Dictionary<string, object>
            {
                ["problemsetQuestionList"] = BuildQuestionListObject(nodeCount, nodes, extraCatalogFields)
            }
        };
        var rawCatalogPage = JsonSerializer.Serialize(catalogData);

        var rawDetailResponses = Enumerable.Range(1, nodeCount)
            .ToDictionary(
                i => $"slug-{i}",
                i => JsonSerializer.Serialize(new
                {
                    data = new
                    {
                        question = new { content = withContent ? $"<p>Content {i}</p>" : (string?)null }
                    }
                }));

        var mappedNodes = Enumerable.Range(1, nodeCount)
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

        return new LeetCodeRawCaptureDto
        {
            RawCatalogPages = [rawCatalogPage],
            RawDetailResponses = rawDetailResponses,
            TotalCount = nodeCount,
            MappedNodes = mappedNodes
        };
    }

    private static object BuildQuestionListObject(
        int total,
        IEnumerable<object> questions,
        Dictionary<string, object>? extraFields)
    {
        var dict = new Dictionary<string, object>
        {
            ["total"] = total,
            ["questions"] = questions
        };
        if (extraFields is not null)
        {
            foreach (var kv in extraFields)
                dict[kv.Key] = kv.Value;
        }
        return dict;
    }

    /// <summary>
    /// Verifies that <c>PersistSnapshotAsync</c> serializes the raw capture (including
    /// <c>rawCatalogPages</c>, <c>rawDetailResponses</c>, and <c>totalCount</c>) and calls
    /// both repository methods with correctly populated data.
    /// </summary>
    [Fact]
    public async Task PersistSnapshotAsync_CallsRepositoryWithCorrectData()
    {
        var capture = BuildCapture(3, withContent: true);
        IngestionSnapshot? capturedSnapshot = null;

        var repo = new Mock<ISnapshotRepository>();
        repo.Setup(r => r.PersistSnapshotAsync(It.IsAny<IngestionSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<IngestionSnapshot, CancellationToken>((s, _) => capturedSnapshot = s)
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.MarkAsLatestAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(repo);
        await service.PersistSnapshotAsync(capture);

        capturedSnapshot.Should().NotBeNull();
        capturedSnapshot!.Source.Should().Be(SnapshotService.SourceIdentifier);
        capturedSnapshot.ProblemCount.Should().Be(3);

        // Payload must round-trip as LeetCodeRawCaptureDto with raw pages and detail responses
        var deserialized = JsonSerializer.Deserialize<LeetCodeRawCaptureDto>(capturedSnapshot.Payload, JsonOpts);
        deserialized.Should().NotBeNull();
        deserialized!.RawCatalogPages.Should().HaveCount(1);
        deserialized.RawDetailResponses.Should().HaveCount(3);
        deserialized.TotalCount.Should().Be(3);

        // MappedNodes is [JsonIgnore] and must not be persisted
        deserialized.MappedNodes.Should().BeEmpty();

        repo.Verify(r => r.MarkAsLatestAsync(capturedSnapshot.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that the persisted payload contains the raw catalog page JSON strings,
    /// not a reconstructed DTO, so the original GraphQL response shape is preserved.
    /// </summary>
    [Fact]
    public async Task PersistSnapshotAsync_PayloadContainsRawCatalogPageJson()
    {
        var capture = BuildCapture(2, withContent: true);

        IngestionSnapshot? capturedSnapshot = null;
        var repo = new Mock<ISnapshotRepository>();
        repo.Setup(r => r.PersistSnapshotAsync(It.IsAny<IngestionSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<IngestionSnapshot, CancellationToken>((s, _) => capturedSnapshot = s)
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.MarkAsLatestAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(repo);
        await service.PersistSnapshotAsync(capture);

        capturedSnapshot.Should().NotBeNull();
        using var doc = JsonDocument.Parse(capturedSnapshot!.Payload);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        doc.RootElement.TryGetProperty("rawCatalogPages", out var pagesProp).Should().BeTrue();
        pagesProp.GetArrayLength().Should().Be(1);
        doc.RootElement.TryGetProperty("rawDetailResponses", out var detailProp).Should().BeTrue();
        doc.RootElement.TryGetProperty("totalCount", out var countProp).Should().BeTrue();
        countProp.GetInt32().Should().Be(2);
    }

    /// <summary>
    /// Verifies that extra/unmapped JSON fields present in the raw catalog page survive the
    /// full persist-then-load snapshot round-trip without being dropped.
    /// </summary>
    [Fact]
    public async Task PersistAndLoad_UnmappedFieldsInRawCatalogPageSurviveRoundTrip()
    {
        // Build a raw catalog page that contains an extra field not modeled in any DTO.
        var nodeWithExtraField = new
        {
            titleSlug = "two-sum",
            frontendQuestionId = "1",
            title = "Two Sum",
            difficulty = "Easy",
            topicTags = new[] { new { name = "Array", slug = "array" } },
            premiumOnly = true  // unmapped — must survive round-trip
        };
        var rawCatalogPage = JsonSerializer.Serialize(new
        {
            data = new
            {
                problemsetQuestionList = new
                {
                    total = 1,
                    questions = new[] { nodeWithExtraField }
                }
            }
        });

        var capture = new LeetCodeRawCaptureDto
        {
            RawCatalogPages = [rawCatalogPage],
            RawDetailResponses = new Dictionary<string, string>
            {
                ["two-sum"] = JsonSerializer.Serialize(new { data = new { question = new { content = "<p>x</p>" } } })
            },
            TotalCount = 1
        };

        IngestionSnapshot? capturedSnapshot = null;
        var repo = new Mock<ISnapshotRepository>();
        repo.Setup(r => r.PersistSnapshotAsync(It.IsAny<IngestionSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<IngestionSnapshot, CancellationToken>((s, _) => capturedSnapshot = s)
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.MarkAsLatestAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(repo);
        await service.PersistSnapshotAsync(capture);

        // Simulate load from the persisted snapshot
        var persistedSnapshot = new IngestionSnapshot
        {
            Id = capturedSnapshot!.Id,
            Source = SnapshotService.SourceIdentifier,
            CapturedAt = capturedSnapshot.CapturedAt,
            ProblemCount = capturedSnapshot.ProblemCount,
            Payload = capturedSnapshot.Payload,
            IsLatest = true
        };
        repo.Setup(r => r.LoadLatestAsync(SnapshotService.SourceIdentifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(persistedSnapshot);

        var loaded = await service.LoadLatestSnapshotAsync();

        loaded.Should().NotBeNull();
        loaded!.RawCatalogPages.Should().HaveCount(1);

        // The unmapped 'premiumOnly' field must be present in the raw page JSON
        using var doc = JsonDocument.Parse(loaded.RawCatalogPages[0]);
        var questionEl = doc.RootElement
            .GetProperty("data")
            .GetProperty("problemsetQuestionList")
            .GetProperty("questions")[0];
        questionEl.TryGetProperty("premiumOnly", out var premiumProp).Should().BeTrue(
            "unmapped field 'premiumOnly' must survive the snapshot persist+load round-trip");
        premiumProp.GetBoolean().Should().BeTrue();
    }

    /// <summary>
    /// Verifies that when a snapshot exists, the raw capture (including catalog pages and
    /// detail responses) is correctly deserialized and returned.
    /// </summary>
    [Fact]
    public async Task LoadLatestSnapshotAsync_WhenSnapshotExists_ReturnsDeserializedCapture()
    {
        var capture = BuildCapture(2, withContent: true);
        // Remove MappedNodes before serializing (as happens during persist)
        var payload = JsonSerializer.Serialize(new LeetCodeRawCaptureDto
        {
            RawCatalogPages = capture.RawCatalogPages,
            RawDetailResponses = capture.RawDetailResponses,
            TotalCount = capture.TotalCount
        }, JsonOpts);

        var snapshot = new IngestionSnapshot
        {
            Id = Guid.NewGuid(),
            Source = SnapshotService.SourceIdentifier,
            CapturedAt = DateTimeOffset.UtcNow.AddHours(-1),
            ProblemCount = capture.TotalCount,
            Payload = payload,
            IsLatest = true
        };

        var repo = new Mock<ISnapshotRepository>();
        repo.Setup(r => r.LoadLatestAsync(SnapshotService.SourceIdentifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var service = CreateService(repo);
        var result = await service.LoadLatestSnapshotAsync();

        result.Should().NotBeNull();
        result!.RawCatalogPages.Should().HaveCount(1);
        result.RawDetailResponses.Should().ContainKey("slug-1");
        result.RawDetailResponses.Should().ContainKey("slug-2");
        result.TotalCount.Should().Be(2);
        // MappedNodes is [JsonIgnore] and should be empty on replay
        result.MappedNodes.Should().BeEmpty();

        // Verify raw detail contains the expected content
        using var detailDoc = JsonDocument.Parse(result.RawDetailResponses["slug-1"]);
        detailDoc.RootElement
            .GetProperty("data")
            .GetProperty("question")
            .GetProperty("content")
            .GetString()
            .Should().Be("<p>Content 1</p>");
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
