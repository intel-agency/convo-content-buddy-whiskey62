using ConvoContentBuddy.Data;
using ConvoContentBuddy.Data.Repositories;
using ConvoContentBuddy.Data.Seeder;
using ConvoContentBuddy.Data.Seeder.Models;
using ConvoContentBuddy.Data.Seeder.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<AppDbContext>("convocontentbuddy",
    configureDbContextOptions: options =>
        options.UseNpgsql(npgsql => npgsql.UseVector()));

// ── Config binding ────────────────────────────────────────────────────────────
builder.Services.Configure<EmbeddingProfileOptions>(
    builder.Configuration.GetSection("EmbeddingProfile"));
builder.Services.PostConfigure<EmbeddingProfileOptions>(opts =>
{
    if (string.IsNullOrEmpty(opts.ApiKey) || opts.ApiKey.StartsWith("${"))
        opts.ApiKey = builder.Configuration["GEMINI_API_KEY"] ?? string.Empty;
});
builder.Services.Configure<LeetCodeClientOptions>(
    builder.Configuration.GetSection("LeetCode"));

// ── Repository registrations ──────────────────────────────────────────────────
builder.Services.AddScoped<IProblemRepository, ProblemRepository>();
builder.Services.AddScoped<ITagRepository, TagRepository>();
builder.Services.AddScoped<IProblemTagRepository, ProblemTagRepository>();
builder.Services.AddScoped<ISnapshotRepository, SnapshotRepository>();

// ── T3 service registrations ──────────────────────────────────────────────────
builder.Services.AddScoped<ISnapshotService, SnapshotService>();
builder.Services.AddHttpClient<ILeetCodeGraphQlClient, LeetCodeGraphQlClient>();
builder.Services.AddScoped<ILeetCodeDataSource, ResilientLeetCodeDataSource>();

// ── Embedding service registrations ──────────────────────────────────────────
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var opts = sp.GetRequiredService<IOptions<EmbeddingProfileOptions>>();
    var logger = sp.GetRequiredService<ILogger<GeminiEmbeddingService>>();
    var config = sp.GetRequiredService<IConfiguration>();
    var maxConcurrency = config.GetValue("Seeder:MaxDegreeOfParallelism", 4);
    return new GeminiEmbeddingService(factory.CreateClient(), opts, logger, maxConcurrency);
});
builder.Services.AddSingleton<IGeminiBatchEmbeddingService>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var opts = sp.GetRequiredService<IOptions<EmbeddingProfileOptions>>();
    var logger = sp.GetRequiredService<ILogger<GeminiBatchEmbeddingService>>();
    return new GeminiBatchEmbeddingService(factory.CreateClient(), opts, logger);
});

builder.Services.AddHostedService<ProblemIngestionWorker>();

var host = builder.Build();
host.Run();
