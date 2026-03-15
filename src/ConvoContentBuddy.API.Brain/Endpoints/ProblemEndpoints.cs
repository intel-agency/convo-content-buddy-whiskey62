using ConvoContentBuddy.API.Brain.Models;
using ConvoContentBuddy.Data;
using ConvoContentBuddy.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Pgvector;

namespace ConvoContentBuddy.API.Brain.Endpoints;

/// <summary>
/// Minimal API route group for problem search endpoints at <c>/api/problems</c>.
/// </summary>
public static class ProblemEndpoints
{
    /// <summary>Maps the <c>/api/problems</c> route group endpoints to the application.</summary>
    /// <param name="app">The web application to map routes onto.</param>
    public static void MapProblemEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/problems");

        group.MapGet("/count", async (
            IProblemRepository repo,
            CancellationToken ct) =>
        {
            var count = await repo.CountAsync(ct);
            return Results.Ok(new { count });
        });

        group.MapGet("/search", async (
            string? q,
            int? limit,
            IProblemRepository repo,
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
            AppDbContext dbContext,
            IOptions<EmbeddingProfileOptions> optionsAccessor,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q))
                return Results.BadRequest(new { error = "Query parameter 'q' is required" });

            var effectiveLimit = Math.Clamp(limit ?? 5, 1, 20);

            var options = optionsAccessor.Value;

            var totalCount = await repo.CountAsync(ct);
            if (totalCount == 0)
                return Results.Json(
                    new { error = "No corpus available for the active embedding profile" },
                    statusCode: 503);

            var hasMatchingCorpus = await dbContext.Problems.AnyAsync(
                p => p.EmbeddingModel == options.ModelName
                     && p.EmbeddingDimensions == options.Dimensions
                     && p.Embedding != null,
                ct);

            if (!hasMatchingCorpus)
                return Results.Json(
                    new { error = "No corpus available for the active embedding profile" },
                    statusCode: 503);

            var generated = await embeddingGenerator.GenerateAsync([q], cancellationToken: ct);
            var queryFloats = generated[0].Vector.ToArray();
            var queryVector = new Vector(queryFloats);

            var results = await repo.SearchByVectorAsync(
                queryVector, options.ModelName, options.Dimensions, effectiveLimit, ct);

            var summaries = results.Select(p => ProblemSummary.FromProblem(p, queryFloats)).ToList();
            return Results.Ok(summaries);
        }).WithName("SearchProblems");
    }
}
